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
  "bank": "otp",
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
Az egyedi konfiguráció válasza a tárolt `bank` értéket is tartalmazza (`otp` vagy `raiffeisen`).

#### `POST /api/configurations/template`

**Request body:**

```json
{
  "name": "Default template",
  "bank": "otp",
  "templateVersion": "template-v2"
}
```

A `name`, `bank` és `templateVersion` mező kötelező. A `bank` értéke `otp` vagy `raiffeisen`; a `name` legfeljebb 100, a `templateVersion` legfeljebb 50 karakter hosszú lehet. A `templateVersion` értékének pontosan egyeznie kell a `ResponseTemplates.JSON` fájlban szereplő verzióval (`template-v1`–`template-v4`); ismeretlen verzió esetén `400 Bad Request` érkezik.

A `template-v4` ügyfélnek címzett választ készít a kiválasztott szabály, a díjértékelés és a források alapján. Nem hívja a `/generate` végpontot, de az ügy előfeldolgozásához továbbra is szükséges az AI-szolgáltatás `/preprocessing` végpontja. A verzió használatához ilyen template-konfigurációt kell választani az üzenet `configurationId` mezőjében.

**Sikeres válasz — `201 Created`:** a létrehozott konfiguráció `ConfigurationResponse` alakban, az előző példában leírt mezőkkel. A válasz `Location` fejlécében a konfiguráció lekérdezési útvonala található.

#### `POST /api/configurations/llm`

**Request body:**

```json
{
  "name": "Default LLM",
  "bank": "otp",
  "promptVersion": "prompt-v1",
  "modelName": "model-name",
  "modelParameters": {
    "temperature": 0.2
  }
}
```

A `name`, `bank`, `promptVersion`, `modelName` és `modelParameters` mező kötelező. A `bank` értéke `otp` vagy `raiffeisen`. A `name` legfeljebb 100, a `promptVersion` legfeljebb 50, a `modelName` legfeljebb 100 karakter hosszú lehet.

**Sikeres válasz — `201 Created`:** a létrehozott konfiguráció `ConfigurationResponse` alakban, az előző példában leírt mezőkkel. A válasz `Location` fejlécében a konfiguráció lekérdezési útvonala található.

#### `POST /api/configurations/rag`

```json
{
  "name": "RAG OTP",
  "bank": "otp",
  "retrievalTopK": 5,
  "retrievalMinSimilarity": 0.4,
  "modelName": "model-name",
  "modelParameters": { "max_new_tokens": 180, "do_sample": false }
}
```

Csak a `name` és a `bank` kötelező; a bank `otp` vagy `raiffeisen`. A `retrievalTopK` pozitív egész, a `retrievalMinSimilarity` 0 és 1 közötti szám. A retrieval- és modellbeállítások elhagyhatók: ilyenkor a Python-szolgáltatás alapértelmezései érvényesülnek. A megadott modellnevet az API a Python `/models` listáján ellenőrzi. A válasz `201 Created`, `generationMode: "Rag"` értékkel. Az új adatbázismezőkhöz az `AddRagPipeline` migráció alkalmazása szükséges.

A konfiguráció az üzenet létrehozásakor a szokásos `configurationId` mezővel választható ki. A RAG-folyamat preprocessing után közvetlenül a `/generate-rag` végpontot hívja, nem használ szabálymotort vagy választervet.

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
      "configurationId": 1,
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
  "configurationId": 1,
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
      "bank": "otp",
      "evaluationTrace": [
        {
          "ruleId": "HU-EXAMPLE-001",
          "ruleName": "Example rule",
          "prioScore": 100,
          "isMatched": false,
          "isSelected": false,
          "allConditionsMatched": false,
          "anyConditionsMatched": null,
          "allConditions": [
            {
              "field": "category",
              "operator": "equals",
              "expected": "SAVINGS_ACCOUNT_FEES",
              "fieldPresent": true,
              "isMatched": false
            }
          ],
          "anyConditions": []
        }
      ]
    },
    "responsePlan": {
      "decision": "Approved",
      "reason": "Matched rule",
      "action": "Provide the applicable guidance.",
      "nextStep": "Ask for any missing case details.",
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

Az eredmény egyes részei még nem állnak rendelkezésre, ezért `null` értékűek lehetnek. Ha egyetlen eredményrész sem érhető el, a `results` értéke `null`, a `resultsAvailable` pedig `false`. A `ruleDecision.reason` az alkalmazott szabály azonosítóját, a teljesült feltételeket és a prioritás szerinti kiválasztás okát írja le; a `ruleDecision.evaluationTrace` minden vizsgált szabályhoz visszaadja a találati/kiválasztási állapotot és a feltételek külön eredményét. A nyomvonal nem tartalmaz tényleges ügyadat-értékeket. A `Rules` rekordban tárolt indoklás a választerv `reason` mezőjében is megjelenik. Ha nincs egyezés, az indoklás ezt és a vizsgált szabályok számát jelzi.

RAG esetén a `ruleDecision` és `responsePlan` `null`, a `response.sources` pedig a visszakeresett dokumentumrészleteket tartalmazza: `sourceId`, `documentName`, `chunkIndex`, `text`, `startChar`, `endChar` és `similarity`. A források az adatbázisban is megmaradnak.

Feldolgozási hiba esetén az `error` alakja:

```json
{
  "type": "Timeout",
  "message": "A feldolgozási hiba leírása"
}
```

A hiba részleges eredményekkel együtt is visszaadható. Az `availabilityMessage` feldolgozási állapottól és az eredmények elérhetőségétől függően tájékoztató szöveget tartalmazhat, vagy `null` lehet.

Az OTP bankkártya-kézbesítési szabály két héten belüli határideje kizárólag projektfeladat-feltételezés, bankfióki megrendelés esetére; nem ellenőrzött OTP-tájékoztatás.

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
  "message": "A feldolgozandó ügyfélüzenet",
  "bank": "otp"
}
```

`bank` értéke `otp` vagy `raiffeisen`, és a konfigurációban kiválasztott szabálykészlet bankját jelöli.

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

### `POST {BaseUrl}/generate-rag`

```json
{
  "message": "A feldolgozott üzenet",
  "bank": "OTP",
  "facts": { "amount": ["100"] },
  "category": "BANK_TRANSFER_FEES",
  "retrieval_top_k": 5,
  "retrieval_min_similarity": 0.4,
  "model_name": null,
  "model_parameters": null
}
```

Az üzenet, tények és kategória a preprocessing eredménye; a bank értéke `OTP` vagy `Raiffeisen`. A sikeres válasz a generált `response` és modellmetaadatok mellett `sources` listát is tartalmazhat, a `reference_similarity` és `reference_similarity_metric` párossal együtt. Hibák kezelése megegyezik a többi AI-végpontéval.

### `POST {BaseUrl}/generate`

**Request body:**

```json
{
  "input_data": {
    "case_state": {},
    "decision": "BankTransferFeesGuidance",
    "reason": "A szabály illeszkedésének technikai leírása",
    "rule_result": {},
    "bank": "otp",
    "response_plan": {
      "decision": "BankTransferFeesGuidance",
      "reason": "A szabály illeszkedésének technikai leírása",
      "required_elements": [],
      "response_structure": {},
      "sources": [],
      "validation_requirements": null
    }
  },
  "message": "A feldolgozott üzenet",
  "facts": {
    "amount": ["100"]
  },
  "category": "BANK_TRANSFER_FEES",
  "rule_decision": "A közölt terhelés magasabb a szabály szerint számított díjnál.",
  "rule_reason": "A megadott adatok alapján a szabály szerinti díj 900 Ft, a közölt terhelés 1000 Ft.",
  "matched_rules": ["HU-BANK-TRANSFER-FEES-001"],
  "prompt_name": "prompt-v1",
  "model_parameters": {
    "temperature": 0.2
  }
}
```

`rule_decision` és `rule_reason` általában a kiválasztott szabály `customerDecision` és `customerReason` mezőiből származik; `matched_rules` a ténylegesen alkalmazott, kiválasztott szabály azonosítóját tartalmazza. OTP átutalási díjnál e két mező a megadott körülmények és a szabály szerinti tarifa összevetésének eredményét tartalmazza, a közölt adatokat igaznak feltételezve. Ha az összevetés nem lehetséges, a `rule_decision` értéke `Rule Not Found`, a `rule_reason` felsorolja a hiányzó adatokat; ez nem jelenti azt, hogy az alkalmazott szabály nem létezik vagy a panasz megalapozatlan. A dátumot jelenleg az `átutalás napja: ÉÉÉÉ-HH-NN` vagy `átutalás dátuma: ÉÉÉÉ-HH-NN` alakban ismeri fel. Ha nincs kiválasztott szabály vagy hiányzik a szabály azonosítója, ügyfélnek szánt döntése vagy indoka, az LLM-feldolgozás hibával leáll. A technikai döntés és illeszkedési indok az `input_data` része marad. A `response_plan` kizárólag az `input_data` objektum része. Ha nincs konfigurált modellnév, a kérés nem tartalmaz `model_name` mezőt, és a Python alapértelmezett modellje érvényesül. Kifejezetten konfigurált modell esetén a .NET ellenőrzi a `/models` választ. A template generációs mód nem hívja ezt az AI-végpontot.

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
HTTP 200 esetén a nem üres `response` sikeres válasz. A Python semleges válaszánál `model_name` lehet `null`, ilyenkor a .NET a választ sikeresen feldolgozza és megjeleníti; a modellhez, promptverzióhoz és modellparaméterekhez tartozó metaadatok is lehetnek null értékűek.

Példa sikeres semleges válaszra, modellhívás nélkül:

```json
{
  "response": "A rendelkezésre álló adatok alapján nem született döntés.",
  "prompt_version": null,
  "model_name": null,
  "model_id": null,
  "model_parameters": null,
  "success": true,
  "reference_similarity": null,
  "reference_similarity_metric": null,
  "error": null
}
```

### AI-szolgáltatás hibái

Az AI-szolgáltatás hibát jelző válaszának `error` objektuma:

```json
{
  "code": "AI_ERROR",
  "message": "A hiba leírása",
  "correlation_id": "correlation-id"
}
```

HTTP 400, 404 és 422 kérés-, modell- vagy promptkonfigurációs hibát jelez; 503 a szolgáltatás elérhetetlenségét, 504 időtúllépést jelent. A .NET ezeket hibaállapotként kezeli, nem válaszként jeleníti meg a hiba JSON-ját. Az API általános, nem technikai hibaüzenetet ad vissza; az AI `error.code` és `error.correlation_id` értékei diagnosztikai metaadatként kerülnek átadásra.

A `code`, `message` és `correlation_id` mezők opcionálisak lehetnek. Nem sikeres HTTP-válasznál a kliens a HTTP-státuszt és — ha elérhető — a hibaválaszt, illetve a `x-correlation-id` fejlécet használja a hiba feldolgozásához.
FastAPI típusú 422-es `detail` lista esetén a diagnosztikai hibaüzenet a hibás mezők útvonalát tartalmazza, a beküldött értékeket nem.

A generálási folyamat célzott tesztjei a projekt gyökeréből futtathatók: `dotnet run --project Tests\GenerationTests.csproj`.

## Nem definiált publikus végpont

A projektben szereplő `RequestProcessing` DTO-hoz nincs publikus processing-létrehozó controller action. A feldolgozás a `POST /api/conversations/{conversationId}/messages` hívásakor indul.
