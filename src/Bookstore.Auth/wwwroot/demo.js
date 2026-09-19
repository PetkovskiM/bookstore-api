"use strict";

const stateKey = "bookstore.oauth.state";
document.getElementById("authorize")?.addEventListener("click", () => {
    const state = crypto.randomUUID();
    sessionStorage.setItem(stateKey, state);
    const parameters = new URLSearchParams({
        client_id: "bookstore-browser",
        response_type: "token",
        redirect_uri: `${location.origin}/demo/callback`,
        scope: "books.search",
        state
    });
    location.assign(`/connect/authorize?${parameters}`);
});

const result = document.getElementById("result");
function completeSignIn() {
    const response = new URLSearchParams(location.hash.slice(1));
    // Remove the fragment immediately; never log or persist an access token.
    history.replaceState(null, "", location.pathname);
    const expectedState = sessionStorage.getItem(stateKey);
    sessionStorage.removeItem(stateKey);
    if (!expectedState || response.get("state") !== expectedState) {
        result.textContent = "This response did not match a sign-in started in this tab. Please try again.";
    } else if (response.has("error") || !response.get("access_token") || response.get("token_type")?.toLowerCase() !== "bearer") {
        result.textContent = "Sign-in could not be completed. Please try again.";
    } else {
        result.textContent = "Sign-in completed and search access was granted.";
    }
    response.delete("access_token");
}

if (result) {
    completeSignIn();
    window.addEventListener("hashchange", completeSignIn);
}
