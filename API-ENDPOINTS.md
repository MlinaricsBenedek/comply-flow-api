# API-végpontok, request body-k és response-ok

Ez a dokumentum a publikus API-végpontokat és az alkalmazás által hívott külső AI-szolgáltatás végpontjait sorolja fel.

## Általános tudnivalók

- A publikus végpontok útvonalai `/api` előtaggal kezdődnek.
- A JSON-példák camelCase mezőneveket használnak.
- A `DateTime` mezők ISO 8601 formátumúak.
- Minden hiba `application/problem+json` típusú ProblemDetails választ ad. A DTO-validációs hibák további `errors` mezőt tartalmaznak.
- A hibaválaszokban szerepel a stabil `code` és kérésenkénti `traceId`; AI-szolgáltatási hibánál az AI korrelációs azonosítója `aiCorrelationId` néven is megjelenhet.
- A `{id}` és `{conversationId}` útvonalparaméterek egész számok.

Példa általános hibaválaszra:

```json
{
  "type": "about:blank",
  "title": "AI service timed out.",
  "status": 504,
  "instance": "/api/conversations/1/messages",
  "code": "ai_service_timeout",
  "traceId": "00-..."
}
```

Az AI-szolgáltatás hibái `504 Gateway Timeout` (`ai_service_timeout`), `503 Service Unavailable` (`ai_service_unavailable`) vagy `502 Bad Gateway` (`ai_service_failure`) választ adnak. Váratlan szerverhiba `500 Internal Server Error` (`internal_error`); a belső kivételüzenet nem kerül a válaszba. A DTO-validáció `400` (`validation_error`), a service-validáció `400` (`invalid_request`), a nem találat pedig `404` (`not_found` vagy a végpontnak megfelelő részletesebb kód). A Processing rekord a hibát ettől függetlenül `Failed` állapottal és hibaadataival rögzíti.

### Request payload naplózás

Az API a bejövő JSON-kéréseket és az AI-szolgáltatás felé elküldött JSON body-kat soronként naplózza a `Logs/api-payloads.jsonl` fájlba. A kapcsolódó bejövő és kimenő naplóbejegyzések közös request ID-t kapnak. A fájl ügyfélüzeneteket is tartalmazhat; kezeld érzékeny adatként, és korlátozd a hozzáférését és megőrzési idejét. A naplózási útvonal a `RequestLogging:FilePath` beállítással módosítható.

## Publikus API

### Konfigurációk

#### `GET /api/configurations`

**Request body:** nincs.

**Sikeres válasz — `200 OK`:**

```json
[
  {
    "id": 1,
    "name": "Default LLM"
  }
]
```

#### `GET /api/configurations/{id}`

**Request body:** nincs.

**Sikeres válasz — `200 OK`:**

```json
{
  "id": 1,
  "name": "Default LLM",
  "ruleSetVersion": "v1",
  "generationMode": "Llm",
  "promptVersion": "prompt-v1",
  "modelName": "model-name",
  "modelParameters": {
    "temperature": 0.2
  }
}
```

Ha nincs ilyen konfiguráció: `404 Not Found`, ProblemDetails válasszal (`configuration_not_found`).

A konfigurációs válasz `templateVersion`, `promptVersion`, `modelName` és `modelParameters` mezői csak akkor szerepelnek, ha értékük nem `null`. A konkrét konfiguráció típusától függően ezek közül eltérő mezők lehetnek jelen.

#### `POST /api/configurations/template`

**Request body:**

```json
{
  "name": "Default template",
  "ruleSetVersion": "v1",
  "templateVersion": "template-v1"
}
```

Mindhárom mező kötelező. A `name` legfeljebb 100, a verziómezők legfeljebb 50 karakter hosszúak lehetnek.

**Sikeres válasz — `201 Created`:** a létrehozott konfiguráció `ConfigurationResponse` alakban, az előző példában leírt mezőkkel. A válasz `Location` fejlécében a konfiguráció lekérdezési útvonala található.

#### `POST /api/configurations/llm`

**Request body:**

```json
{
  "name": "Default LLM",
  "ruleSetVersion": "v1",
  "promptVersion": "prompt-v1",
  "modelName": "model-name",
  "modelParameters": {
    "temperature": 0.2
  }
}
```

Minden mező kötelező. A `name` legfeljebb 100, a `ruleSetVersion` és `promptVersion` legfeljebb 50, a `modelName` legfeljebb 100 karakter hosszú lehet.

**Sikeres válasz — `201 Created`:** a létrehozott konfiguráció `ConfigurationResponse` alakban, az előző példában leírt mezőkkel. A válasz `Location` fejlécében a konfiguráció lekérdezési útvonala található.

### Beszélgetések és üzenetek

#### `GET /api/conversations`

**Request body:** nincs.

**Sikeres válasz — `200 OK`:**

```json
[
  {
    "id": 1,
    "title": "Ügyfélkérés",
    "status": "Created",
    "createdAt": "2026-10-26T00:00:00Z"
  }
]
```

#### `POST /api/Conversation/conversation`

Az ASP.NET Core útvonalkezelése nem különbözteti meg a kis- és nagybetűket; az útvonal kisbetűs alakban is használható.

**Request body:**

```json
{
  "title": "Ügyfélkérés"
}
```

A `title` kötelező, legfeljebb 200 karakter hosszú lehet.

**Sikeres válasz — `201 Created`:**

```json
{
  "id": 1,
  "title": "Ügyfélkérés",
  "status": "Created",
  "createdAt": "2026-10-26T00:00:00Z"
}
```

A válasz `Location` fejlécében az új beszélgetés lekérdezési útvonala található.

#### `GET /api/Conversation/conversation/{id}`

**Request body:** nincs.

**Sikeres válasz — `200 OK`:** egy beszélgetés, a fenti példában látható alakban.

Ha nincs ilyen beszélgetés: `404 Not Found`, ProblemDetails válasszal (`conversation_not_found`).

#### `GET /api/conversations/{conversationId}/messages`

**Request body:** nincs.

**Sikeres válasz — `200 OK`:** a beszélgetés feldolgozásai időrendben, kérés–válasz párokként:

```json
{
  "conversationId": 1003,
  "messages": [
    {
      "processingId": 42,
      "status": "Completed",
      "request": {
        "id": 10,
        "content": "Szia, szeretném visszakapni a pénzem.",
        "createdAt": "2026-10-26T00:00:00+00:00"
      },
      "response": {
        "content": "Kérjük, küldje el a vásárlást igazoló bizonylatot.",
        "generationMode": "Llm",
        "createdAt": "2026-10-26T00:00:05+00:00"
      },
      "error": null
    }
  ]
}
```

Folyamatban lévő vagy sikertelen feldolgozásnál a `response` lehet `null`; hiba esetén az `error` tartalmazza a hiba típusát és üzenetét. Érvényes, de még üres beszélgetés esetén `200 OK` válasz és üres `messages` lista érkezik. Nem létező beszélgetésnél `404 Not Found`, ProblemDetails válasszal.

#### `POST /api/conversations/{conversationId}/messages`

**Request body:**

```json
{
  "content": "Szeretnék tájékoztatást kérni...",
  "configurationId": 1
}
```

A `content` kötelező, a `configurationId` értékének legalább 1-nek kell lennie. A hívás feldolgozást is indít.

**Sikeres válasz — `201 Created`:**

```json
{
  "id": 10,
  "conversationId": 1,
  "processingId": 42,
  "role": "User",
  "content": "Szeretnék tájékoztatást kérni...",
  "createdAt": "2026-10-26T00:00:00Z"
}
```

**Lehetséges hibaválaszok:**

| HTTP státusz | Válaszbody |
|---|---|
| `400 Bad Request` | ProblemDetails; üres tartalom: `code: "invalid_message_content"`. DTO-validáció: `code: "validation_error"` és `errors`. |
| `404 Not Found` | ProblemDetails; a kód `conversation_not_found` vagy `configuration_not_found`. |
| `504 Gateway Timeout` | AI-szolgáltatás időtúllépése: `code: "ai_service_timeout"`. |
| `503 Service Unavailable` | AI-szolgáltatás nem érhető el: `code: "ai_service_unavailable"`. |
| `502 Bad Gateway` | AI-szolgáltatási hiba vagy hibás válasz: `code: "ai_service_failure"`. |
| `500 Internal Server Error` | Nem várt hiba: `code: "internal_error"`; a belső kivételüzenet nincs kitéve. |

### Feldolgozási eredmény

Az alábbi két GET útvonal ugyanazt az eredményt adja:

- `GET /api/processings/{processingId}`
- `GET /api/processing/GetProcessingResult/{processingId}`

**Request body:** nincs.

- `200 OK`: a feldolgozás befejeződött.
- `202 Accepted`: a feldolgozás még folyamatban van.
- `404 Not Found`: nincs ilyen feldolgozás, ProblemDetails válasszal (`processing_not_found`).

**Sikeres vagy folyamatban lévő válasz — `ProcessingResultResponse`:**

```json
{
  "id": 42,
  "status": "Completed",
  "createdAt": "2026-10-26T00:00:00Z",
  "startedAt": "2026-10-26T00:00:01Z",
  "finishedAt": "2026-10-26T00:00:05Z",
  "isFinished": true,
  "resultsAvailable": true,
  "availabilityMessage": null,
  "error": null,
  "results": {
    "preProcessing": {
      "cleanedText": "Tisztított szöveg",
      "caseType": "Example",
      "structuredCaseStateJson": "{\"category\":\"Example\"}"
    },
    "ruleDecision": {
      "decision": "Approved",
      "reason": "Matched rule",
      "ruleSetVersion": "v1",
      "matchedRules": [
        {
          "matched": true,
          "prioScore": 10,
          "resultJson": "{\"decision\":\"Approved\"}",
          "source": null,
          "effectiveFrom": null,
          "reason": "rule-id: Rule name"
        }
      ]
    },
    "responsePlan": {
      "decision": "Approved",
      "reason": "Matched rule",
      "requiredElementsJson": "[]",
      "responseStructureJson": "{}",
      "sourcesJson": "[]"
    },
    "response": {
      "text": "Generált válasz",
      "generationMode": "Llm",
      "templateVersion": null,
      "promptVersion": "prompt-v1",
      "modelName": "model-name",
      "processingTimeMs": 1200
    },
    "evaluationResults": [
      {
        "metric": "reference_similarity",
        "score": 0.92,
        "unit": "0-1",
        "comment": "similarity metric"
      }
    ]
  }
}
```

Az eredmény egyes részei még nem állnak rendelkezésre, ezért `null` értékűek lehetnek. Ha egyetlen eredményrész sem érhető el, a `results` értéke `null`, a `resultsAvailable` pedig `false`. Feldolgozási hiba esetén az `error` alakja:

```json
{
  "type": "Timeout",
  "message": "A feldolgozási hiba leírása"
}
```

A hiba részleges eredményekkel együtt is visszaadható. Az `availabilityMessage` feldolgozási állapottól és az eredmények elérhetőségétől függően tájékoztató szöveget tartalmazhat, vagy `null` lehet.

## Külső AI-szolgáltatás

Az alábbi relatív útvonalakat az alkalmazás az `AiService:BaseUrl` konfigurációs értékhez fűzi hozzá. Ezeket az alkalmazás hívja; nem publikus, az API-kliensek által közvetlenül meghívandó végpontok.

### `GET {BaseUrl}/models`

**Request body:** nincs.

**Sikeres válasz body:**

```json
{
  "models": [
    {
      "name": "model-name",
      "is_default": true
    }
  ],
  "success": true,
  "error": null
}
```

A `models` lista nem lehet üres; minden modell neve kötelező és egyedi, valamint legfeljebb egy modell lehet alapértelmezett.

### `POST {BaseUrl}/preprocessing`

**Request body:**

```json
{
  "message": "A feldolgozandó ügyfélüzenet"
}
```

**Sikeres válasz body:**

```json
{
  "message": "Tisztított üzenet",
  "facts": {
    "amount": ["100"],
    "date": ["2026-10-01"]
  },
  "category": "Example",
  "success": true,
  "partial_success": false,
  "warnings": [],
  "warning_details": {},
  "error": null
}
```

Részleges siker esetén a `partial_success` értéke `true`, és a `warnings` lista nem lehet üres. A `warning_details` opcionális objektum; a figyelmeztetéskódokhoz tartozó részletes információkat tartalmazza.

### `POST {BaseUrl}/generate`

**Request body:**

```json
{
  "input_data": {
    "case_state": {},
    "decision": "Approved",
    "reason": "Matched rule",
    "rule_result": {}
  },
  "message": "A feldolgozott üzenet",
  "facts": {
    "amount": ["100"]
  },
  "category": "Example",
  "prompt_name": "prompt-v1",
  "model_name": "model-name",
  "model_parameters": {
    "temperature": 0.2
  }
}
```

**Sikeres válasz body:**

```json
{
  "response": "A generált szöveg",
  "prompt_version": "prompt-v1",
  "model_name": "model-name",
  "model_id": "model-id",
  "model_parameters": {
    "temperature": 0.2
  },
  "success": true,
  "reference_similarity": 0.92,
  "reference_similarity_metric": "similarity metric",
  "error": null
}
```

A `reference_similarity` és a hozzá tartozó `reference_similarity_metric` együtt szerepelhetnek, vagy mindkettő hiányozhat/null lehet.

### AI-szolgáltatás hibái

Az AI-szolgáltatás hibát jelző válaszának `error` objektuma:

```json
{
  "code": "AI_ERROR",
  "message": "A hiba leírása",
  "correlation_id": "correlation-id"
}
```

A `code`, `message` és `correlation_id` mezők opcionálisak lehetnek. Nem sikeres HTTP-válasznál a kliens a HTTP-státuszt és — ha elérhető — a hibaválaszt, illetve a `x-correlation-id` fejlécet használja a hiba feldolgozásához.

## Nem definiált publikus végpont

A projektben szereplő `RequestProcessing` DTO-hoz nincs publikus processing-létrehozó controller action. A feldolgozás a `POST /api/conversations/{conversationId}/messages` hívásakor indul.
