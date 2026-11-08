# SRS – Comply Flow API (megvalósult rendszer)

## 1. Cél és hatókör

Ez a dokumentum a repositoryban található implementáció aktuális állapotát írja le: a rendszer célját, üzleti folyamatait, publikus HTTP API-ját, külső AI-szolgáltatás-hívásait és perzisztencia-viselkedését. A leírás kódból és az `OTP.JSON`, illetve `Raiffeisen.JSON` szabályfájlokból készült; nem jövőbeli követelménylista.

A Comply Flow API beszélgetésekben fogad ügyfélüzeneteket, konfigurációt rendel a feldolgozáshoz, külső AI-szolgáltatással előfeldolgoztatja az üzenetet, szabályokat értékel ki, majd generált választ ment. A válasz és a feldolgozás rész-/végeredménye a beszélgetési előzmény vagy a Processing-lekérdező végponton kérhető le.

## 2. Rendszerkörnyezet és architektúra

- ASP.NET Core Web API, C# / .NET 10, nullable reference types engedélyezve.
- Entity Framework Core és SQL Server.
- Rétegek: Controller → Service → Repository → `AppDbContext` → SQL Server.
- A `ProcessingService` integrálja a fájlból olvasott szabálykészletet, a `RuleEngine`-t és az `IAiServiceClient`-et.
- A fő belépési pont a `Program.cs`. A repository-k, service-ek és HTTP-kliens DI-n keresztül kerülnek regisztrálásra.
- Fejlesztői környezetben érhető el az OpenAPI és a Swagger UI. HTTPS-átirányítás be van kapcsolva.
- A `DefaultConnection` kapcsolat hiányában az alkalmazás induláskor hibával leáll. Az EF Core migrációk a forrásban elérhetők, de az alkalmazás induláskor nem futtat automatikus migrációt; az adatbázis sémáját telepítéskor külön kell naprakészen tartani.

## 3. Domain objektumok és állapotok

### 3.1 Conversation

Egy beszélgetés `Id`, opcionális `Title`, `Status`, `CreatedAt` és kapcsolódó üzenet-/feldolgozásgyűjtemények adatait tartalmazza. Létrehozáskor a cím körüli szóközök levágódnak, a státusz `Created`, az idő UTC. A publikus lekérdezések nem módosítják a beszélgetés státuszát.

### 3.2 Message

Az üzenet a beszélgetéshez, szerephez (`Role`), tartalomhoz és UTC létrehozási időhöz kötött. A beérkező üzenetek szerepe `User`, tartalmuk szélső whitespace-e levágódik. A Processing az `InputMessageId` kapcsolattal hivatkozik rá; a `Message.ProcessingId` egy külön, opcionális kapcsolat a Processinghez tartozó további üzenetek számára, ezt a jelenlegi folyamat nem használja.

### 3.3 Configuration

Két létrehozási mód van:

- `Template`: `Name`, `Bank`, `RuleSetVersion`, `TemplateVersion`.
- `Llm`: `Name`, `Bank`, `RuleSetVersion`, `PromptVersion`, `ModelName`, `ModelParameters`.

A szöveges értékek tárolás előtt trimmelődnek. Az LLM paraméter-szótár JSON-szövegként kerül az adatbázisba. A konfiguráció generációs módja és időbélyege szerveroldali érték (`Template`/`Llm`, UTC). A konfiguráció csak feldolgozási futáskor kapcsolódik a `Processing` rekordhoz; létrehozáskor a hivatkozott szabálykészlet vagy AI-modell meglétét nem ellenőrzi a rendszer.

### 3.4 Processing

Egy feldolgozás beszélgetéshez, bemeneti üzenethez és konfigurációhoz tartozik. Státuszai:

`Created → Processing → Completed`

vagy

`Created → Processing → Failed`

Más státuszátmenet tiltott. A kezdési és befejezési UTC időpontot az átmenet rögzíti. A hibatípus és -üzenet a sikertelen feldolgozáson marad; a már mentett rész-eredmények is elérhetők lehetnek.

## 4. Üzleti működés

### 4.1 Beszélgetés és üzenetfolyam

1. Beszélgetés létrehozásakor a service elutasítja az üres/whitespace címet, trimmeli a címet, majd létrehozza az alapadatokat.
2. Üzenet beküldésekor a service ellenőrzi a tartalmat, majd a beszélgetés meglétét.
3. Lekéri a kiválasztott konfigurációt. Hiányzó beszélgetés vagy konfiguráció esetén a megfelelő `404` válasz érkezik; még nincs sikeres üzenet-/Processing-mentés.
4. Létrehozza a `User` üzenetet és a hozzá tartozó `Created` állapotú Processinget. A repository egy `SaveChangesAsync` hívással menti az új Processinget és a navigációs kapcsolaton keresztül az új bemeneti üzenetet.
5. A teljes feldolgozás ugyanazon HTTP-kérés során, szinkron módon fut végig. Nincs háttérsor/feladatindító. Siker esetén a POST csak a feldolgozás befejezése után adja vissza a `201 Created` választ; futás közben egy másik lekérdezés még `202 Accepted` státuszt láthat.

### 4.2 Processing lépései

1. A Processing `Processing` státuszba lép, elmentődik a `StartedAt`.
2. A konfiguráció `Bank` értéke alapján a `TextFileService` beolvassa az `OTP.JSON` vagy `Raiffeisen.JSON` fájlt a konfigurált üzleti szabálykönyvtárból. A JSON-nak verziózott szabálykészlet-listának kell lennie.
3. A `ProcessingService` a kiválasztott fájlon belül a konfiguráció `RuleSetVersion` értékével pontos, kis-/nagybetű-érzékeny egyezést keres. Pontosan egy szabálykészlet szükséges; hiányzó vagy többszörös egyezés hibát okoz. A kiválasztott készlet alapvető szerkezeti, feltétel- és eredményvalidáción esik át.
4. A szolgáltatás elküldi az eredeti üzenetet az AI-szolgáltatás `/preprocessing` végpontjára. A válasz tisztított üzenetet, kategóriát, tényeket és opcionális részleges sikerre vonatkozó figyelmeztetéseket tartalmaz. A részleges siker figyelmeztetései alkalmazáslogba kerülnek.
5. A kategória és tények `StructuredCaseState` állapotot alkotnak. A kategória `category` mezőként kerül bele; az egyértékű tény lista skalárként, a többelemes lista tömbként kerül be. A RuleEngine kiértékeli a kiválasztott szabálykészlet szabályait.
6. A legmagasabb prioritású egyező szabály adja a döntést és az indoklást. Ha nincs egyező szabály, a döntés `NoMatch`, az indoklás `No rule matched the preprocessed case.`. A preprocessing, a döntés, az alkalmazott bank és szabálykészlet-verzió, valamint a választerv adatbázisba mentődik; az egyes vizsgált szabályok találati állapota nem kerül eltárolásra.
7. A rendszer lekéri az elérhető modelleket a `/models` végpontról. Ha a konfiguráció modellneve nincs megadva, az alapértelmezett modellt, ennek hiányában az első modellt választja; megadott névnél kis-/nagybetűtől független egyezést keres. Hiányzó modell hibát okoz.
8. A `/generate` kérés `input_data` objektuma tartalmazza a `case_state`, `decision`, `reason`, `rule_result` mezőket. Emellett átadja a tisztított üzenetet, tényeket, kategóriát, a konfigurált promptverziót (Template módban annak hiányában a sablonverziót), a feloldott modellnevet és a konfigurált modellparamétereket.
9. A generált válasz, a használt generációs mód/verziók/modell, a kérési kontextus és az eltelt idő elmentődik. Ha az AI válasza `reference_similarity` értéket és a hozzá tartozó metrikanevet is ad, azok `EvaluationResult` rekordként is mentődnek.
10. A Processing `Completed` állapotba lép. Bármely feldolgozási kivételnél a service megpróbálja `Failed` állapotban elmenteni a hiba típusát, szövegét és befejezési idejét, naplózza a hibát, majd továbbdobja azt. A publikus üzenet-POST nem alakítja az ilyen kivételt saját, domain-specifikus válasszá; általános HTTP-hibakezelés érvényesül.

### 4.3 RuleEngine és bankonkénti JSON-fájlok

- A konfiguráció `bank` értéke választja ki a `OTP.JSON` vagy `Raiffeisen.JSON` fájlt; a `ruleSetVersion` ezután a kiválasztott fájlon belül azonosítja a használt szabálykészletet.
- A `bank` csak `otp` vagy `raiffeisen` lehet. A kiválasztott bank és a ténylegesen alkalmazott `ruleSetVersion` a feldolgozás szabálydöntés-rekordjában tárolódik és a feldolgozási eredményben jelenik meg.
- A szabály feltételei `all` és/vagy `any` listák. Mindkét lista esetén az `all` minden feltétele ÉS kapcsolatban áll az `any` legalább egy feltételével; csak `all` esetén minden feltételnek teljesülnie kell; csak `any` esetén legalább egynek. Üres `all` igaz, üres `any` hamis; mindkét lista hiánya feltétel nélküli szabályt jelent.
- A támogatott operátorok: `equals`, `notequals`, `in`, `notin`, `greaterthan`, `greaterthanorequal`, `lessthan`, `lessthanorequal`. Az operátor neve trimmelődik és kis-/nagybetűtől független.
- A `StructuredCaseState` kulcsai kis-/nagybetűtől függetlenül kereshetők; a mezőnév közvetlen kulcs, a pontozott/nested útvonal-felbontás nincs támogatva. Hiányzó mező nem egyező.
- Az egyenlőség csak azonos típusú szöveg, logikai vagy numerikus értékek között működik. Szövegegyezés kis-/nagybetű-érzékeny; számok numerikusan hasonlítódnak. Két explicit `null` egyenlő. Nem kompatibilis típus esetén a `notequals` sem igaz. A tényleges tömb értékei közül bármely egyezés elegendő `equals`/`in` esetén; `notequals`/`notin` esetén minden tényleges értéknek összehasonlíthatónak kell lennie, és egyik sem egyezhet.
- A sorrendi operátorok kizárólag numerikus értékeket hasonlítanak. Nincs szöveges rendezés vagy szövegből számkonverzió.
- Minden szabály külön kiértékelődik; a kiválasztott szabály a legmagasabb `PrioScore`. Azonos pontszámnál a fájlsorrendben előbbi egyező szabály nyer. A `effectiveFrom` metaadat, nem időbeli érvényességi szűrő.
- A szabályfájlok adatigényeket és válasziránymutatást határoznak meg; a RuleEngine önmagában nem számol díjat, nem értelmez jogi hatályt és nem generál felhasználói választ.
- A `OTP.JSON` és `Raiffeisen.JSON` fájlok szabályai eltérő banki forrásokon alapulhatnak. A konfiguráció bankválasztása megakadályozza, hogy az egyik bank szabályai a másik bank feldolgozására alkalmazódjanak.
- A szabálydöntés részlete nem sorolja fel a vizsgált szabályokat. Csak az alkalmazott bankot és szabálykészlet-verziót, a döntést és indoklást adja vissza.
- A kiválasztott szabály `result` objektumának `requiredElements` tömbjét a választerv explicit adatként használja, ha jelen van. Ennek hiányában a válaszstruktúra levélmező-útvonalai adják a szükséges elemeket. A `source` és `effectiveFrom` bekerül a választerv forráslistájába.
- A `TextFileService.GetPromptAsync` TXT-fájlokat tud beolvasni a prompt könyvtárból, de a jelenlegi feldolgozási folyamat nem hívja; a promptot a konfiguráció verzióazonosítója adja át a külső generálásnak.

### 4.4 Választerv és eredmények lekérése

A Processing eredmény-lekérdezése betölti a preprocessinget, az alkalmazott bankot és szabálykészlet-verziót tartalmazó szabálydöntést, a választervet, a generált választ és a metrikákat. A szabálydöntés nem tartalmazza a vizsgált szabályok listáját. `ResultsAvailable` igaz, ha legalább egy eredményrész létezik; ilyenkor az egyes hiányzó részek `null` értékűek lehetnek. Ha egyik sem létezik, `Results` `null`. A `Created`/`Processing` állapotú feldolgozás nem befejezett; a `Completed` és `Failed` befejezett. A `Failed` állapot részleges eredményt és hibaobjektumot is visszaadhat.

A beszélgetés üzenet-előzménye Processingenként, létrehozási idő és azonosító szerint növekvően adja vissza a bemeneti kérést, az esetleg elkészült választ, státuszt és hibát. A válasz időbélyege a Processing befejezési ideje, nem külön válasz-létrehozási idő.

## 5. Publikus HTTP API

Az útvonalparaméterek egész számok. JSON-mezőnevek camelCase alakban szerepelnek. Az ASP.NET Core útvonalillesztése kis-/nagybetűtől független. Az `[ApiController]` a DataAnnotations DTO-hibákat automatikusan `400 Bad Request` válaszként kezeli; külön domain hibaválasz-kezelő middleware nincs konfigurálva.

### 5.1 Konfigurációk

| Metódus és útvonal | Bemenet | Eredmény |
|---|---|---|
| `GET /api/configurations` | Nincs body. | `200 OK`, lista csak `id` és `name` mezőkkel, legújabbtól a legrégebbi felé. |
| `GET /api/configurations/{id}` | Nincs body. | `200 OK` részletes konfiguráció; hiányzó azonosító: `404`. |
| `POST /api/configurations/template` | `name` (max. 100), `bank` (`otp` vagy `raiffeisen`), `ruleSetVersion` és `templateVersion` (max. 50); mind kötelező. | `201 Created`, részletes konfiguráció és `Location` fejléc. |
| `POST /api/configurations/llm` | `name`/`modelName` (max. 100), `bank` (`otp` vagy `raiffeisen`), `ruleSetVersion`/`promptVersion` (max. 50), `modelParameters` objektum; mind kötelező. | `201 Created`, részletes konfiguráció és `Location` fejléc. |

A részletes válasz az `id`, `name`, `bank`, `ruleSetVersion`, `generationMode` mezők mellett a nem null `templateVersion`, `promptVersion`, `modelName`, `modelParameters` értékeket tartalmazza. A listázás `CreatedAt`, majd `Id` szerint csökkenő.

### 5.2 Beszélgetések és üzenetek

| Metódus és útvonal | Bemenet | Eredmény |
|---|---|---|
| `GET /api/conversations` | Nincs body. | `200 OK`, beszélgetéslista (`id`, `title`, `status`, `createdAt`), legújabbtól a legrégebbi felé. |
| `POST /api/Conversation/conversation` | `{ "title": "..." }`; kötelező, max. 200 karakter. | `201 Created`, beszélgetés és `Location` fejléc. |
| `GET /api/Conversation/conversation/{id}` | Nincs body. | `200 OK` beszélgetés; hiányzó azonosító: `404`. |
| `POST /api/conversations/{conversationId}/messages` | `{ "content": "...", "configurationId": 1 }`; tartalom kötelező, konfigurációazonosító legalább 1. | Siker: a teljes szinkron feldolgozás után `201 Created`, az üzenet, Processing- és konfigurációazonosító. Domainhibák: üres tartalom `400`, nem létező beszélgetés vagy konfiguráció `404`. Feldolgozási kivételnél a Processing hibaként mentődik, majd a kivétel továbbdobódik. |
| `GET /api/conversations/{conversationId}/messages` | Nincs body. | `200 OK`, Processingenkénti kérés-válasz előzmény, üres beszélgetésnél üres lista; nem létező beszélgetés: `404`. |

Az üzenet-létrehozási siker válasza `id`, `conversationId`, `processingId`, `configurationId`, `role`, `content`, `createdAt` mezőket tartalmaz. Az előzmény egy `conversationId` és `messages` listából áll; a listaelem `processingId`, `configurationId`, `status`, `request`, nullázható `response` és nullázható `error` mezőkkel rendelkezik.

### 5.3 Processing eredmény

Mindkét útvonal ugyanazt a választ adja:

- `GET /api/processings/{processingId}`
- `GET /api/processing/GetProcessingResult/{processingId}`

Nincs request body. Nem létező azonosító: `404 Not Found`; még be nem fejezett feldolgozás: `202 Accepted`; befejezett (`Completed` vagy `Failed`) feldolgozás: `200 OK`.

A válasz fő mezői: `id`, `status`, `createdAt`, `startedAt`, `finishedAt`, `isFinished`, `resultsAvailable`, `availabilityMessage`, `error` és `results`. Az eredményrészek: `preProcessing` (tisztított szöveg, kategória, strukturált állapot JSON-szöveg), `ruleDecision` (döntés, indoklás, alkalmazott bank és szabálykészlet-verzió), `responsePlan`, `response` (szöveg, mód, verziók, modell, feldolgozási idő), `evaluationResults`.

## 6. Külső AI-szolgáltatás API

Az útvonalak az `AiService:BaseUrl` abszolút HTTP(S) URL-hez fűződnek. A BaseUrl konfiguráció kötelező és induláskor validálódik. A `HttpClient` általános timeoutja végtelen; a hívások saját megszakítási ideje konfigurálható, 1–3600 másodperc közötti értékkel. Alapértelmezés: `/models` 10 mp, `/preprocessing` 60 mp, `/generate` 180 mp.

| Metódus és útvonal | Küldött adatok | Várt válasz és alkalmazáslogika |
|---|---|---|
| `GET /models` | Nincs body. | `success: true` és nem üres `models` lista kell; modellnév kötelező/egyedi, legfeljebb egy `is_default` modell lehet. |
| `POST /preprocessing` | `{ "message": "..." }` | Siker esetén nem üres `message`, `facts` objektum és opcionális `category`; a részleges sikerhez nem üres `warnings` szükséges. A `facts` kulcsai és elemei nem lehetnek üresek. |
| `POST /generate` | `input_data`, `message`, `facts`, `category`, `prompt_name`, `model_name`, `model_parameters`. | Siker esetén nem üres válasz/prompt/model/model ID és modellparaméterek szükségesek. A `reference_similarity` 0–1 közötti lehet; a metrika neve csak vele együtt szerepelhet. |

Az AI-hívások nem publikus, kliens által közvetlenül használandó API-végpontok. Az AI-hiba `AiServiceException`-ként különbözteti meg az elérhetetlenséget, időtúllépést, HTTP-hibát, távoli szolgáltatáshibát és érvénytelen választ. A HTTP-hiba válaszából az `error` objektum és/vagy az `x-correlation-id` fejléc korrelációs azonosítója felhasználható. A feldolgozási hibát a service a Processing rekordban tárolja.

## 7. Adatbázis-kezelés és perzisztencia

### 7.1 Entitások és kapcsolatok

| Entitás | Tárolt szerep |
|---|---|
| `Conversation` | Beszélgetés azonosítója, címe, státusza és időbélyege. |
| `Message` | Beszélgetéshez tartozó üzenet, szerep, szöveg, időbélyeg és opcionális Processing-kapcsolat. |
| `Configuration` | Bank, szabálykészlet-/generálási mód és prompt-, sablon-, modellbeállítások. |
| `Processing` | Bemeneti üzenet, beszélgetés, konfiguráció, státusz/idők és hiba. |
| `PreProcessing` | Tisztított szöveg, kategória és strukturált állapot JSON-szövege. |
| `Rules` | Döntés és indoklás, alkalmazott bank és kiválasztott szabálykészlet-verzió. |
| `ResponsePlan` | Döntés, indoklás, szükséges elemek, válaszstruktúra és források JSON-szövegként. |
| `Response` | Generált szöveg, mód/verziók/modell, paraméterek, input kontextus és feldolgozási idő. |
| `EvaluationResult` | Metrika, pontszám, mértékegység és opcionális megjegyzés. |

Kapcsolatok: Conversation 1:N Message és 1:N Processing; Configuration 1:N Processing; Processing N:1 bemeneti Message; Processing 1:N kapcsolt Message (opcionális `Message.ProcessingId`); Processing 1:0..1 PreProcessing, Rules, ResponsePlan, Response; Processing 1:N EvaluationResult. A konfigurációra, bemeneti üzenetre és beszélgetésre mutató idegen kulcsokat a Processing tárolja. Az `AppDbContext` valamennyi felsorolt kapcsolatnál `NoAction` törlési viselkedést állít be; kaszkád törlés nincs.

### 7.2 Mentés és lekérdezés

- A repository-k EF Core-on keresztül érik el az adatbázist; az egyszerű olvasások `AsNoTracking()` módúak.
- A Conversation- és Configuration-listák időbélyeg, majd ID szerint csökkenően rendezettek. Nincs lapozás.
- A Processing eredmény betöltése split query-val tölti be a kapcsolódó eredménytáblákat.
- A Processing státusz- és eredménylépések külön `SaveChangesAsync` műveletekkel mentődnek, így hiba esetén a már elmentett rész-eredmények megmaradhatnak.
- A hibás Processing státuszát a repository közvetlen SQL `ExecuteUpdateAsync` művelettel állítja `Failed`-re, a hiba- és befejezési adatokkal együtt.
- A listázás, üzenetküldés és státuszlekérés kivételével nincs publikus törlő vagy módosító végpont; megőrzési/archiválási művelet nem implementált.
- Az adatbázisban található szabálykészletet nem tárol a rendszer; azt minden Processing futás fájlból olvassa be.

## 8. Fájlok, konfiguráció és naplózás

- `TextFiles:BusinessRulesDirectory` az `OTP.JSON` és `Raiffeisen.JSON` fájlok könyvtárát adja meg. Relatív útvonal a content roothoz viszonyul; hiányzó beállítás, könyvtár vagy bankhoz tartozó fájl hibát okoz. Mindkét szabályfájl build/publish kimenetbe másolódik.
- `TextFiles:PromptDirectory` TXT promptfájlok könyvtára; a fájlokat a service fájlnév szerint rendezi és összefűzi, de a jelenlegi Processing-folyamat nem használja ezt az olvasót. A `TextFileService` mindkét könyvtárbeállítást a példányosításakor feloldja, így mindkét konfigurációs érték szükséges; hiányzó TXT-fájl csak a promptolvasó meghívásakor okozna hibát.
- A `RequestPayloadLoggingMiddleware` minden kéréshez JSON body-t olvas, a streamet visszaállítja, majd a kérést továbbengedi. A JSON request body-k és a külső AI felé menő POST body-k JSONL sorokként naplózódnak, közös HTTP-kérésazonosítóval.
- Az alapértelmezett naplófájl `Logs/api-payloads.jsonl`; a `RequestLogging:FilePath` felülírhatja. Az AI válaszbody-kat a payload logger nem rögzíti. Fájlrendszeri I/O vagy hozzáférési hiba naplózódik, és nem szakítja meg a kérést.
- A payload napló ügyfélüzenetet, illetve AI-nak küldött adatot tartalmazhat; érzékeny adatként kezelendő, hozzáférését és megőrzését korlátozni kell.
- A `CancellationToken` a HTTP és adatbázis műveletekbe továbbadódik. Az AI-hívásoknál konfigurált időkorlát külön kezelt; klienskérés megszakítása feldolgozási hibát eredményezhet, ha a Processing már létrejött.

## 9. Nem funkcionális jellemzők, korlátok és hiányzó funkciók

- Az `[ApiController]` és az alap DTO DataAnnotations validáció biztosított; teljes, központosított domain-validációs/hibaformátum nincs.
- Az alkalmazás regisztrál `UseAuthorization()` middleware-t, de nincs autentikációs séma, autentikációs middleware vagy endpoint-szintű jogosultságvédelem konfigurálva. Az API ezért nem tekinthető hitelesítettnek.
- Központi exception handler egységes ProblemDetails választ ad. Az AI-időtúllépés `504`, az AI-szolgáltatás elérhetetlensége `503`, más AI-szolgáltatási hiba `502`, a hibás kérés `400`, a váratlan hiba pedig `500`. A kliens által megszakított kérésre nem próbál választ írni. A hibaobjektum `code` és `traceId` mezőt tartalmaz; belső kivételüzenet nem kerül ki. Az ismeretlen útvonalak és üres státuszválaszok is ProblemDetails formátumot kapnak.
- A POST üzenetvégpont teljes feldolgozást végez a HTTP kérés részeként; hosszú AI-válasz esetén a kliensnek nyitva kell tartania a kérést. Tartós háttérfeldolgozás, újrapróbálkozási sor és idempotenciakulcs nincs.
- A konfiguráció `GenerationMode` értékét menti a rendszer, de a jelenlegi ProcessingService mindkét konfigurációs módban meghívja a `/models` és `/generate` végpontot. A `TemplateVersion` csak prompt fallbackként kerül felhasználásra, külön template-renderelő nincs.
- A preprocessing figyelmeztetések alkalmazáslogba kerülnek, de külön eredménytáblában vagy publikus válaszmezőben nem szerepelnek.
- Nincs felhasználó-/tenantkezelés, autentikáció, rate limiting, publikus törlés, lapozás vagy adatmegőrzési automatizmus.

## 10. Kapcsolódó dokumentáció

Az útvonalak teljes request/response példái, valamint az AI API JSON-szerződései az [API-ENDPOINTS.md](./API-ENDPOINTS.md) dokumentumban találhatók. Ez az SRS azok üzleti és technikai működését, valamint a perzisztencia részleteit foglalja össze.
