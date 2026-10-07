(() => {
'use strict';
let conversationId = null;
let busy = false;
const form = document.getElementById('aiChatForm');
const input = document.getElementById('aiChatMessage');
const send = document.getElementById('aiChatSend');
const reset = document.getElementById('aiChatReset');
const messages = document.getElementById('aiChatMessages');
const status = document.getElementById('aiChatStatus');
const error = document.getElementById('aiChatError');
const panel = document.getElementById('aiAssistantPanel');
const launcher = document.getElementById('aiAssistantLauncher');
const openButtons = document.querySelectorAll('[data-ai-open-assistant]');
let previousFocus = launcher;
function setPanelOpen(open) {
  panel.hidden = !open;
  openButtons.forEach(button => button.setAttribute('aria-expanded', String(open)));
  launcher.hidden = open;
  if (open) input.focus();
  else previousFocus.focus();
}
openButtons.forEach(button => button.addEventListener('click', () => {
  previousFocus = button;
  setPanelOpen(true);
}));
document.getElementById('aiCloseAssistant').addEventListener('click', () => setPanelOpen(false));
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && !panel.hidden) {
    event.preventDefault();
    setPanelOpen(false);
  }
});
function addMessage(role, text, sources = []) {
  const article = document.createElement('article');
  article.className = role;
  const label = document.createElement('strong');
  label.textContent = role === 'user' ? 'You' : 'Assistant';
  const body = document.createElement('p');
  body.textContent = text; // Never render model or retrieved content as HTML.
  article.append(label, body);
  for (const source of sources) {
    const reference = document.createElement('p');
    reference.className = 'source';
    reference.textContent = `Source: ${source.title} · ${source.section} · ${source.sourceReference}`;
    article.append(reference);
  }
  messages.append(article);
  messages.scrollTop = messages.scrollHeight;
}
form.addEventListener('submit', async event => {
  event.preventDefault();
  const message = input.value.trim();
  if (busy || !message) return;
  busy = true; send.disabled = true; reset.disabled = true;
  status.textContent = 'Checking approved information…'; error.textContent = '';
  addMessage('user', message); input.value = '';
  try {
    const response = await fetch('/api/chat/message', {
      method: 'POST', credentials: 'same-origin', headers: {'Content-Type': 'application/json'},
      body: JSON.stringify({message, conversationId}), signal: AbortSignal.timeout(125000)
    });
    let data;
    try { data = await response.json(); } catch { throw new Error('The service returned an unexpected response.'); }
    if (!response.ok) {
      if (data.error?.code === 'conversation_unavailable') conversationId = null;
      throw new Error(data.error?.message || 'The request failed. Check your authentication or try again later.');
    }
    if (typeof data.message !== 'string' || typeof data.conversationId !== 'string' || !Array.isArray(data.sources)) throw new Error('The service returned an unexpected response.');
    conversationId = data.conversationId;
    addMessage('assistant', data.message, data.sources);
  } catch (failure) {
    error.textContent = failure instanceof Error ? failure.message : 'The service is unavailable.';
  } finally {
    busy = false; send.disabled = false; reset.disabled = false;
    status.textContent = ''; if (!panel.hidden) input.focus();
  }
});
input.addEventListener('keydown', event => {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {event.preventDefault(); form.requestSubmit();}
});
reset.addEventListener('click', () => {
  if (busy) return;
  conversationId = null; messages.replaceChildren(); error.textContent = '';
  addMessage('assistant', 'New conversation. What would you like to verify?'); input.focus();
});

})();
