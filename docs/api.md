# API contract

## POST /api/chat/message

Authenticated JSON request, max body 8192 bytes; message 1–2000 characters. Unknown JSON fields (including customerId) are rejected. `conversationId` is optional/null or an existing opaque 32-hex ID owned by the same user, account scope and roles.

```json
{"message":"Where is order 45821?","conversationId":null}
```

```json
{"conversationId":"opaque-id","message":"Fictional prototype data: ...","requestId":"correlation-id","sources":[]}
```

Source entries contain documentId, title, section, lastUpdated and sourceReference. References are display metadata; clients must not execute/render retrieved content as HTML.

Errors use `{"error":{"code":"not_found","message":"The requested information is unavailable."},"requestId":"..."}`. Statuses: 400 invalid request; 401 unauthenticated; 404 missing/inaccessible resource or conversation; 429 rate limit; 503 provider/dependency/internal failure, invalid/unknown model tools or iteration limit; 504 operation timeout. JWT authentication challenges may have an empty body. Error bodies never contain stack traces, provider bodies, SQL details or secrets. Clients must handle request/network cancellation and non-JSON errors from proxies safely.

GET /internal/health is authenticated; it checks process readiness only. In production deployment it must also be restricted at the ingress. The prototype is Development-only. GET /openapi/v1.json and demo assets are Development-only. Business tools have no public routes. Default limits: 20 chat requests per user per minute, 16 concurrent HTTP requests, no waiting queue.

No API supports writes or accepts trusted authorization scope from the browser. JWT authority/audience and account_id/role claims are provisional integration contracts. DevAuth is explicit server-side mock identity and must remain on loopback.
