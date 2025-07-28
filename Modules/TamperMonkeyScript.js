// ==UserScript==
// @name         BypassRedirect
// @namespace    http://tampermonkey.net/
// @version      2025-07-28
// @description  Redirect completion results to local server
// @author       https://github.com/RiisDev
// @match        https://bypass.city/bypass?bypass=*
// @icon         https://www.google.com/s2/favicons?sz=64&domain=bypass.city
// @grant        none
// ==/UserScript==

(function () {
    'use strict';

    const targetText = "The resolved url is:";

    // Encode to base64 URL-safe format
    function base64UrlEncode(input) {
        return btoa(input)
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=+$/, '');
    }

    function extractResolvedUrl() {
        const paragraphs = document.querySelectorAll("p");
        for (const p of paragraphs) {
            if (p.textContent.includes(targetText)) {
                const bypassedUrl = p.textContent.replace(targetText, "").trim();

                const urlParams = new URLSearchParams(window.location.search);
                const requestUrl = urlParams.get("bypass") || window.location.href;

                const encodedRequestUrl = base64UrlEncode(requestUrl);
                const encodedBypassedUrl = base64UrlEncode(`https://${bypassedUrl}`);

                const redirectUrl = `https://localhost/bypass-city/${encodedRequestUrl}/${encodedBypassedUrl}`;
                console.log("🔁 Redirecting to:", redirectUrl);
                window.location.href = redirectUrl;

                return;
            }
        }
    }

    let int = setInterval(function() {
        if (document.title.includes("Bypass complete")) {
            extractResolvedUrl();
        }
    }, 250)

})();