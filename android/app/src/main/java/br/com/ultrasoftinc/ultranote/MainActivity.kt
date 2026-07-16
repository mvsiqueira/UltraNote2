package br.com.ultrasoftinc.ultranote

import android.app.Activity
import android.app.Dialog
import android.app.DownloadManager
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.os.Message
import android.webkit.CookieManager
import android.webkit.URLUtil
import android.webkit.ValueCallback
import android.webkit.WebChromeClient
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.ProgressBar
import androidx.activity.OnBackPressedCallback
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout

/**
 * Single-Activity WebView wrapper around the UltraNote web app. Deliberately thin: all the
 * real UI/logic lives in the Blazor app itself (see ../../UltraNote.UI). This class only
 * handles the plumbing a plain WebView doesn't do out of the box — file picking, downloads,
 * popups, and back-button navigation.
 */
class MainActivity : AppCompatActivity() {

    private lateinit var webView: WebView
    private var filePickerCallback: ValueCallback<Array<Uri>>? = null
    private var popupDialog: Dialog? = null

    private val filePickerLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        val callback = filePickerCallback
        filePickerCallback = null
        if (callback == null) return@registerForActivityResult
        val data = result.data
        val uris: Array<Uri> = when {
            result.resultCode != Activity.RESULT_OK || data == null -> emptyArray()
            data.clipData != null -> Array(data.clipData!!.itemCount) { i -> data.clipData!!.getItemAt(i).uri }
            data.data != null -> arrayOf(data.data!!)
            else -> emptyArray()
        }
        callback.onReceiveValue(uris)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        val swipeRefresh = findViewById<SwipeRefreshLayout>(R.id.swipeRefresh)
        val progressBar = findViewById<ProgressBar>(R.id.progressBar)
        webView = findViewById(R.id.webView)

        CookieManager.getInstance().setAcceptCookie(true)
        CookieManager.getInstance().setAcceptThirdPartyCookies(webView, true)

        webView.settings.apply {
            javaScriptEnabled = true
            domStorageEnabled = true
            javaScriptCanOpenWindowsAutomatically = true
            setSupportMultipleWindows(true)
            // Google Identity Services refuses to run in a WebView it can detect as one — the
            // detection keys off the "; wv" token Android inserts into the UA string. Stripping
            // it is the standard, widely-used fix (not full UA spoofing: the rest of the string,
            // including the Chrome/WebView version, stays intact).
            userAgentString = userAgentString.replace("; wv", "")
        }

        webView.webViewClient = object : WebViewClient() {
            override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
                val url = request.url
                // Keep http/https navigation inside the app (this covers the UltraNote domain
                // itself and every Google auth/consent host — trying to allowlist those by name
                // is brittle since Google uses several). Hand off anything else (mailto:, tel:,
                // intent:, market:, ...) to whatever app on the device handles it.
                if (url.scheme == "http" || url.scheme == "https") return false
                return try {
                    startActivity(Intent(Intent.ACTION_VIEW, url))
                    true
                } catch (e: Exception) {
                    false
                }
            }

            override fun onPageFinished(view: WebView, url: String) {
                swipeRefresh.isRefreshing = false
                CookieManager.getInstance().flush()
            }
        }

        webView.webChromeClient = object : WebChromeClient() {
            override fun onProgressChanged(view: WebView, newProgress: Int) {
                progressBar.progress = newProgress
                progressBar.visibility = if (newProgress >= 100) android.view.View.GONE else android.view.View.VISIBLE
            }

            override fun onShowFileChooser(
                webView: WebView,
                filePathCallback: ValueCallback<Array<Uri>>,
                fileChooserParams: FileChooserParams
            ): Boolean {
                filePickerCallback?.onReceiveValue(null)
                filePickerCallback = filePathCallback
                filePickerLauncher.launch(fileChooserParams.createIntent())
                return true
            }

            // WebView doesn't support real multi-window, but the Google sign-in popup needs a
            // genuine one: it expects to call window.close() on itself once the flow finishes
            // and signal back to the page that opened it. Collapsing the popup into the main
            // WebView (an earlier version of this) broke that handshake — the popup had nothing
            // left to close, so the login flow visibly hung even though it had already
            // succeeded. A real (if minimal) popup window, dismissed via onCloseWindow, fixes it.
            override fun onCreateWindow(
                view: WebView,
                isDialog: Boolean,
                isUserGesture: Boolean,
                resultMsg: Message
            ): Boolean {
                val popup = WebView(this@MainActivity)
                popup.settings.javaScriptEnabled = true
                popup.settings.domStorageEnabled = true
                popup.settings.javaScriptCanOpenWindowsAutomatically = true
                popup.settings.setSupportMultipleWindows(true)
                popup.settings.userAgentString = webView.settings.userAgentString

                val dialog = Dialog(this@MainActivity, android.R.style.Theme_Black_NoTitleBar_Fullscreen)
                dialog.setContentView(popup)
                popupDialog = dialog

                popup.webChromeClient = object : WebChromeClient() {
                    override fun onCloseWindow(window: WebView) {
                        dialog.dismiss()
                        popupDialog = null
                    }
                }
                popup.webViewClient = WebViewClient()

                val transport = resultMsg.obj as WebView.WebViewTransport
                transport.webView = popup
                resultMsg.sendToTarget()
                dialog.show()
                return true
            }
        }

        webView.setDownloadListener { url, _, contentDisposition, mimeType, _ ->
            try {
                val request = DownloadManager.Request(Uri.parse(url))
                val cookie = CookieManager.getInstance().getCookie(url)
                if (cookie != null) request.addRequestHeader("cookie", cookie)
                request.addRequestHeader("User-Agent", webView.settings.userAgentString)
                val fileName = URLUtil.guessFileName(url, contentDisposition, mimeType)
                request.setMimeType(mimeType)
                request.setDestinationInExternalPublicDir(android.os.Environment.DIRECTORY_DOWNLOADS, fileName)
                request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED)
                request.setTitle(fileName)
                (getSystemService(DOWNLOAD_SERVICE) as DownloadManager).enqueue(request)
            } catch (e: Exception) {
                android.widget.Toast.makeText(this, "Não foi possível baixar o arquivo.", android.widget.Toast.LENGTH_SHORT).show()
            }
        }

        swipeRefresh.setOnRefreshListener { webView.reload() }

        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                if (webView.canGoBack()) webView.goBack() else {
                    isEnabled = false
                    onBackPressedDispatcher.onBackPressed()
                }
            }
        })

        if (savedInstanceState == null) {
            webView.loadUrl(getString(R.string.site_url))
        } else {
            webView.restoreState(savedInstanceState)
        }
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        webView.saveState(outState)
    }

    override fun onDestroy() {
        filePickerCallback?.onReceiveValue(null)
        filePickerCallback = null
        popupDialog?.dismiss()
        popupDialog = null
        super.onDestroy()
    }
}
