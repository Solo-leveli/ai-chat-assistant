# Embed the assistant in a web application

The demo is a sample host page with an assistant icon opening a nonmodal side panel. It is not an integration into Microsoft Teams. The existing real application source is not in this repository.

For a plain HTML application on the same origin:

1. Copy `wwwroot/chat.css` and `wwwroot/chat.js` into your application's static assets. Include the stylesheet and deferred script once, using the correct paths for your application.
2. Copy the complete `<div class="ai-chat-widget">…</div>` block from `wwwroot/index.html` just before the host page's closing `</body>` tag. Preserve the markup and all `ai`-prefixed IDs; use one widget per page. Do not copy the sample workspace cards or `workspace.css` into your real application.
3. Optionally add a header button with `data-ai-open-assistant`, `aria-controls="aiAssistantPanel"`, `aria-expanded="false"` and a visible/accessible label.
4. Serve the chat API at `/api/chat/message` on the same origin, either in the host backend or through an authenticated reverse proxy. The sample widget does not establish a BOLT session or inject bearer tokens. Integrate the existing app's authenticated request mechanism server-side and confirm account claims before production. Never expose Development auth on a public application.
5. Verify the launch button, keyboard focus, Escape/close behavior, mobile layout and API error states in your actual app.

Styles for the assistant are scoped beneath `.ai-chat-widget`; the script runs inside a closure. The host app's styles should still be checked for broad overriding selectors. Assistant/user/document text is rendered with textContent, never HTML. Closing the panel retains the current conversation in memory; New conversation resets it. Closing during a request does not cancel the request and the result remains available when reopening.

For React, Angular, Vue or another framework, adapt the markup and lifecycle into the existing framework rather than injecting a second framework or duplicating widgets on navigation. Framework-specific integration needs the host application's source.

The UI change does not enable a real AI model. The current demo still uses deterministic Mock AI and fictional logistics data.
