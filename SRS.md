# SRS – Jelenlegi állapot

## 1. Bevezetés

Ez a dokumentum a Comply Flow API projekt aktuális, meglévő állapotát írja le. A cél, hogy a rendszer jelenlegi funkcionalitását, architektúráját, adatmodelljét és korlátait dokumentálja, a jövőbeli fejlesztések alapjául szolgálva.

A dokumentum nem egy teljes „future-state” specifikáció, hanem a tényleges implementáció alapján készült állapotleírás.

## 2. A rendszer célja

A rendszer egy ASP.NET Core Web API, amely:

- beszélgetéseket és üzeneteket kezel,
- konfigurációkat hoz létre különböző szabályhalmaz- és generációs módokhoz,
- feldolgozási folyamatokat köt a felhasználói üzenetekhez,
- szabályalapú döntési logikát támogat a RuleEngine segítségével,
- strukturált adatmodellt használ a szabályértékeléshez és a beszélgetési folyamatok nyomon követéséhez.

A projekt kulcsfontosságú domain elemei a következők:

- Conversation (beszélgetés)
- Message (üzenet)
- Configuration (konfiguráció)
- Processing (feldolgozás)
- RuleEngine / Rule / Condition / ConditionGroup

## 3. Jelenlegi technológiai környezet

A projekt jelenlegi infrastruktúrája a következő:

- ASP.NET Core Web API
- C# / .NET 10
- Entity Framework Core
- SQL Server
- Swagger / OpenAPI
- Repository + Service réteg architektúra

A fő belépési pont a Program.cs fájl, ahol a DI, adatbázis-konfiguráció, vezérlők regisztrálása és middleware konfiguráció történik.

## 4. Rendszerarchitektúra

A projekt a repository-service-controller stratégiát követi.

### 4.1 Rétegek

1. Controller réteg
   - API endpointok a Controllers könyvtárban.
   - Felelős a HTTP kérés-válasz kezeléséért.

2. Service réteg
   - Műveleti logika és üzleti szabályok.
   - A ConversationService, ConfigurationService és ProcessingService kezeli a fő funkciókat.

3. Repository réteg
   - Adatkezelés a DAL felé.
   - A ConversationRepository, ConfigurationRepository és ProcessingRepository a konkrét perzisztencia műveleteket végzi.

4. Adatmodell és EF Core
   - AppDbContext definiálja az entitások és kapcsolatok konfigurációját.
   - SQL Server adatbázist használ.

## 5. Jelenlegi domain modell

### 5.1 Conversation

A Conversation entitás a beszélgetés fő objektuma.

Tulajdonságok:
- Id
- Title
- Status
- CreatedAt
- Messages
- Processings

A rendszer képes beszélgetést létrehozni, lekérni az összeset és egy adott azonosítóhoz tartozót.

### 5.2 Message

A Message az egyes felhasználói vagy rendszerbeli üzenetet reprezentálja.

Tulajdonságok:
- Id
- ConversationId
- Role
- Content
- CreatedAt
- ProcessingId
- Conversation
- Processing
- InputForProcessings

Az üzenet tartalmát a rendszer egész egyszerűen stringként tárolja és validálja, hogy ne legyen üres.

### 5.3 Configuration

A Configuration a feldolgozási konfigurációt írja le, mely lehet:
- Template alapú konfiguráció
- LLM alapú konfiguráció

Tulajdonságok:
- Id
- Name
- RuleSetVersion
- GenerationMode
- TemplateVersion
- PromptVersion
- ModelName
- ModelParametersJson
- CreatedAt

A konfigurációk generációs módja jelenleg a következő értékekkel dolgozik:
- Template
- Llm

### 5.4 Processing

A Processing a feldolgozási folyamatot reprezentálja, amely mindig egy konkrét beszélgetéshez és konfigurációhoz kapcsolódik.

Tulajdonságok:
- Id
- ConversationId
- InputMessageId
- ConfigurationId
- Status
- ErrorType
- ErrorMessage
- CreatedAt
- StartedAt
- FinishedAt
- Messages
- PreProcessing
- Rules
- ResponsePlan
- Response
- EvaluationResults

Ez az entitás a feldolgozás általános műveleti egysége. A feldolgozást a ProcessingService koordinálja.

Státusz-életciklus:
- Új feldolgozás `Created` státuszban jön létre.
- A feldolgozás megkezdésekor `Processing` állapotba lép.
- Sikeres befejezéskor `Completed`, hiba esetén `Failed` állapotba lép.
- Csak a `Created -> Processing`, `Processing -> Completed` és `Processing -> Failed` átmenetek megengedettek.
- A `Completed` és `Failed` terminális állapotok; ismételt futtatáshoz új Processing rekord szükséges.

A ProcessingService az alábbi lépéseket hajtja végre:
- Processing rekord létrehozása és `Processing` státuszra váltás.
- A konfigurált szabálykészlet verziójának ellenőrzése.
- Üzenet előfeldolgozása a Python AI API `/preprocessing` végpontján.
- A preprocessingből származó tények kiértékelése a RuleEngine segítségével.
- Választerv és szabálykiértékelések mentése.
- Modell lekérése a `/models` végpontról, majd válasz generálása a `/generate` végponton.
- A válasz és a generálási metaadatok mentése, végül `Completed` státusz.
- Feldolgozási hiba esetén `Failed` státusz.
- A hiba típusa és üzenete a Processing rekordban tárolódik; a kezdési és befejezési időpontok is rögzülnek.

### 5.5 RuleEngine modell

A rendszer támogatja a szabályalapú döntéshozatalt a következő modellekkel:
- Condition
- ConditionGroup
- Rule
- RuleEvaluationResult
- RuleEngineResult
- StructuredCaseState

A RuleEngine képes:
- értéket lekérni egy strukturált állapotból,
- feltételeket kiértékelni,
- több feltétel között AND/OR logikát alkalmazni,
- szabályokat értékelni és a legmagasabb prioritású egyező szabályt kiválasztani.

A támogatott operátorok például:
- equals
- notequals
- in
- notin
- greaterthan
- greaterthanorequal
- lessthan
- lessthanorequal

## 6. Jelenlegi funkcionális követelmények

### FR-01 – Beszélgetés létrehozása
A rendszer lehetővé teszi új beszélgetés létrehozását a következő adatokkal:
- cím
- státusz
- létrehozási idő

A beszélgetés létrehozását a ConversationController és ConversationService végzi.

### FR-02 – Beszélgetések listázása
A rendszer megjeleníti az összes beszélgetést.

### FR-03 – Beszélgetés lekérdezése azonosító alapján
A rendszer lekéri a megadott ID-vel rendelkező beszélgetést.

### FR-04 – Üzenet létrehozása beszélgetéshez
A rendszer lehetővé teszi üzenet hozzáadását egy meglévő beszélgetéshez.

A létrehozás során a rendszer ellenőrzi:
- hogy a beszélgetés létezik,
- hogy a konfiguráció létezik,
- hogy az üzenet tartalma nem üres,
- hogy a feldolgozási folyamat létrejöhet-e.

### FR-05 – Konfiguráció létrehozása sablon alapján
A rendszer képes template típusú konfigurációt létrehozni.

Bekért adatok:
- név
- rule set verzió
- template verzió

### FR-06 – Konfiguráció létrehozása LLM alapján
A rendszer képes LLM típusú konfiguráció objektum létrehozására.

Bekért adatok:
- név
- rule set verzió
- model neve
- prompt verzió
- model paraméterek JSON formában

### FR-07 – Konfigurációk listázása
A rendszer listázza a meglévő konfigurációkat rövid összefoglaló formában.

### FR-08 – Konfiguráció lekérdezése azonosító alapján
A rendszer visszaad egy konfigurációt ID alapján.

### FR-09 – Feldolgozási rekord létrehozása üzenethez
Amikor üzenet jön létre, a rendszer automatikusan létrehoz egy Processing rekordot, amely a beszélgetéshez, konfigurációhoz és bemeneti üzenethez kapcsolódik.

### FR-10 – Szabályértékelés
A RuleEngine képes feltételek és szabálycsoportok kiértékelésére, majd kiválasztja a legmagasabb prioritású egyező szabályt.

## 7. Jelenlegi API felület vázlata

### 7.1 Conversation API

- GET /api/conversations
  - Összes beszélgetés lekérdezése

- POST /api/conversations/conversation
  - Beszélgetés létrehozása

- GET /api/conversations/conversation/{id}
  - Beszélgetés lekérdezése ID alapján

- POST /api/conversations/{conversationId}/messages
  - Üzenet küldése egy beszélgetéshez

### 7.2 Configuration API

- GET /api/configurations
  - Konfigurációk listázása

- GET /api/configurations/{id}
  - Konfiguráció lekérdezése ID alapján

- POST /api/configurations/template
  - Template konfiguráció létrehozása

- POST /api/configurations/llm
  - LLM konfiguráció létrehozása

### 7.3 Processing API

- GET /api/processing/GetProcessingResult/{processingId}
  - Processing státusz, hiba és életciklus-időbélyegek lekérdezése

## 8. Adatbázis és kapcsolati modell

Az EF Core modellezés szerint a fő kapcsolatok:

- Conversation 1:N Message
- Conversation 1:N Processing
- Processing N:1 Configuration
- Processing N:1 InputMessage
- Processing 1:1 PreProcessing
- Processing 1:1 Rules
- Rules 1:N MatchedRules
- Processing 1:1 ResponsePlan
- Processing 1:1 Response
- Processing 1:N EvaluationResult

Ez a modell jól támogatja a feldolgozási és szabályértékelési folyamatok nyomon követését, azonban a teljes üzleti logika még nem teljesen implementált az API és a szolgáltatási rétegben.

## 9. NEM funkcionális követelmények

### NFR-01 – Adatbázis-integráció
A rendszernek SQL Serverrel kell működnie, és EF Core-en keresztül kell kapcsolatot létesítenie a perzisztencia réteggel.

### NFR-02 – REST API kompatibilitás
A rendszer RESTful endpointokat használ, és a JSON konvenciók mellett működik.

### NFR-03 – Swagger dokumentáció
A projekt Swagger/OpenAPI támogatást használ a fejlesztési környezetben, így a végpontok dokumentálhatók és tesztelhetők.

### NFR-04 – Validáció
A rendszer legalább alapvető validációt végez:
- üres beszélgetés cím tiltása,
- üres üzenet tartalom tiltása,
- konfiguráció megléte ellenőrzése a feldolgozás létrehozásakor.

### NFR-05 – Biztonság
Jelenleg nincs autentikáció vagy autorizáció beépítve, ezért a rendszer a jelenlegi állapotában nem alkalmas bizalmas környezetben történő produkciós használatra.

## 10. Jelenlegi hiányosságok és technikai adósságok

A projekt jelenlegi állapota figyelemre méltó, de számos fejlesztési hiányosság is látszik:

1. A ProcessingController még nincs tényleges implementálva.
2. A feldolgozási eredmények lekérdezési flowja még nem működik teljesen.
3. Nincs autentikáció és jogosultságkezelés.
4. Nincs teljes hibakezelő middleware vagy globális exception handling.
5. A feldolgozási folyamat a konfigurált szabálykészlet-verziót ellenőrzi, de a szabálykészlet jelenleg egy konfigurált `Rules.JSON` fájlból töltődik be.
6. A Python AI API elérhetősége szükséges az előfeldolgozáshoz, a modellek lekéréséhez és a válaszgeneráláshoz.
7. A bemeneti DTO-k több endpointon tartalmaznak alapvető DataAnnotations-validációt, de nincs egységes, minden domain-szabályra kiterjedő validációs és hibaválasz-rendszer.
8. A dokumentáció és a funkcionalitás közötti egyezőség részben még fejlesztés alatt áll.

## 11. Jelenlegi rendszerhatár és korlátok

A rendszer jelenleg a következő elsődleges funkciókra specializálódott:
- beszélgetés és üzenetkezelés,
- konfiguráció létrehozása és lekérdezése,
- feldolgozási rekordszintű nyomon követés,
- szabályalapú értékelés logika.

A következő funkciók még nem látszanak teljes körűen megvalósítva:
- végső válaszgenerálás,
- automatikus döntési folyamat végrehajtása a Processing alapján,
- villámgyors adatvizualizáció,
- felhasználói jogosultságok és felhőalapú API hozzáférés kezelése.

## 12. Következtetés

A Comply Flow API jelenlegi állapota egy működő alaparchitektúrájú, adatbázis-alapú REST API, amely képes beszélgetések, üzenetek és konfigurációk kezelésére, valamint szabályalapú feldolgozási logikát támogat. A projekt stabil alapokra épül, de a teljes üzleti folyamat és a végpontok teljes körű integrációja még nem zárult le.

A rendszer jövőbeli fejlesztési iránya főként a következő területek felé mutat:
- teljes Processing életciklus végrehajtása,
- szabály és válaszgenerálás automatizálása,
- biztonságosabb autentikáció és hitelesítés,
- API teljes validációja és hibakezelése,
- finomhangolt, produkcióra alkalmas adat- és környezetspecifikáció.

## 13. Rövid változat (projekt állapot összefoglaló)

A projekt jelenleg egy ASP.NET Core Web API, amely:
- beszélgetéseket és üzeneteket kezel,
- konfigurációkat hoz létre sablon vagy LLM módon,
- Processing rekordokat köt a beszélgetésekhez,
- szabályértékelési logikát tartalmaz a RuleEngine-en keresztül,
- SQL Serverrel dolgozik EF Core segítségével,
- Swagger dokumentációt használ fejlesztési célokra,
- még nem teljes körűen implementált végső feldolgozási eredmény- és biztonsági réteggel rendelkezik.

## 14. A backend részletes működése

### 14.1 Indulás és függőség-injektálás

A `Program.cs` a `DefaultConnection` nevű kapcsolati karakterláncból SQL Server adatbázist konfigurál az `AppDbContext` számára. Ha ez a beállítás hiányzik, az alkalmazás induláskor `InvalidOperationException` kivétellel leáll. A repository-k, service-ek és a `TextFileService` scoped élettartammal kerülnek a DI-konténerbe. A kontrollerek és az OpenAPI/Swagger komponensek szintén regisztrálva vannak.

Fejlesztői környezetben az alkalmazás kiteszi az OpenAPI dokumentumot, és bekapcsolja a Swagger UI-t. Minden környezetben HTTPS-átirányítás történik, ezután az autorizációs middleware és a controller route-ok következnek. Autentikációs middleware vagy hitelesítési séma nincs konfigurálva, így az autorizáció jelenléte önmagában nem véd endpointokat.

### 14.2 Felelősségi rétegek

- **Controller:** HTTP útvonalat és státuszkódot rendel a kéréshez, meghívja a megfelelő service-t, majd DTO-t vagy hibaválaszt ad vissza. Itt található a HTTP-specifikus viselkedés.
- **Service:** összeállítja az üzleti műveletet, validálja a művelethez szükséges adatokat, entitásokat hoz létre, és DTO-választ képez.
- **Repository:** az `AppDbContext` segítségével végzi az EF Core adatbázis-műveleteket. A listázó és azonosító szerinti olvasó lekérdezések `AsNoTracking()`-et használnak.
- **Entity/DbContext:** az adatbázisban tárolt objektumokat és azok kapcsolatait írja le.

A controller általában nem fér közvetlenül az adatbázishoz. A `ConversationService` viszont használja a `ProcessingService`-t is: ezért egy üzenet létrehozása a conversation és a processing repository-t is érinti.

### 14.3 Beszélgetés létrehozása és lekérése

1. A `POST /api/Conversation/conversation` kérés a `RequestConversation` DTO-ban egy `Title` értéket fogad.
2. Az `[ApiController]` miatt a DataAnnotations-validáció hibája automatikusan `400 Bad Request` választ eredményez. A service ezen felül elutasítja az üres vagy csak szóközből álló címet.
3. A `ConversationService` levágja a cím körüli szóközöket, a státuszt `Created` értékre, a létrehozási időt pedig UTC-re állítja.
4. A repository beszúrja az entitást, majd `SaveChangesAsync`-szel menti. Siker esetén a controller `201 Created` választ ad a létrejött beszélgetés DTO-jával és a lekérdezési útvonallal.

A `GET /api/conversations` időrendben csökkenő sorrendben listázza a beszélgetéseket; azonos létrehozási idő esetén az ID szerinti csökkenő sorrend a másodlagos rendezés. A `GET /api/Conversation/conversation/{id}` azonosító alapján keres, és nem találatkor `404 Not Found` választ ad. A visszaadott DTO a beszélgetés alapadatait tartalmazza, üzenetlista betöltése nem történik. Az ASP.NET Core útvonal-illesztése nem érzékeny a kis- és nagybetűkre.

### 14.4 Üzenet létrehozása és Processing indítása

A `POST /api/conversations/{conversationId}/messages` folyamat a backend legfontosabb összekapcsolt művelete:

1. A kérés a `RequestMessage` DTO-n keresztül `Content` és `ConfigurationId` mezőt vár. A DTO az üres/hiányzó tartalmat és az 1-nél kisebb konfigurációazonosítót is jelzi; az `[ApiController]` az érvénytelen DTO-t automatikusan `400` válasszal kezeli.
2. A `ConversationService` a service-szinten is elutasítja az üres vagy csak whitespace tartalmat.
3. A service lekéri a beszélgetést. Ha nincs ilyen azonosító, a vezérlő `404` választ ad.
4. Létrejön a `User` szerepű, levágott tartalmú, UTC időbélyegű `Message` entitás.
5. A `ProcessingService` megkeresi a kért konfigurációt. Hiányzó konfiguráció esetén a művelet `ConfigurationNotFound` eredménnyel tér vissza; a controller `404` választ ad, és nem küldi sikeresnek az üzenet létrehozását.
6. Ha a konfiguráció megvan, a service `Created` státuszú `Processing` entitást hoz létre, amely hivatkozik a beszélgetésre, az input üzenetre és a konfigurációra.
7. A `ProcessingRepository` az entitásgráfot egy `SaveChangesAsync` hívással menti. Az új `Message` a `Processing.InputMessage` navigációs tulajdonságán keresztül kerül a mentett gráfba; az adatbázis által kiosztott azonosítókat a válasz DTO-ja már tartalmazza.
8. Siker esetén a végpont `201 Created` státusszal küldi vissza az üzenet azonosítóját, beszélgetés-azonosítóját, Processing-azonosítóját, szerepét, tartalmát és létrehozási idejét.

Ez jelenleg rekord-létrehozási folyamat: a Processing `Created` státusszal jön létre, de utána nem indul el háttérfeladat, szabálykiértékelés, generálás vagy státuszfrissítés.

### 14.5 Konfigurációk

A `POST /api/configurations/template` a név, szabálykészlet-verzió és sablonverzió alapján `Template` módú konfigurációt hoz létre. A `POST /api/configurations/llm` a név, szabálykészlet-verzió, promptverzió, modellnév és paraméter-szótár alapján `Llm` módú konfigurációt hoz létre. Mindkét esetben a service levágja a szöveges mezők széleit, UTC létrehozási időt állít be, majd a repository menti az entitást.

Az LLM-paraméterek a tárolás előtt JSON-szöveggé szerializálódnak (`ModelParametersJson`), egyedi modellhívás vagy LLM SDK használata azonban nincs a jelenlegi folyamatban. A részletes konfigurációs válasz a JSON-paramétereket újra objektummá deszerializálja; a konfigurációlista csak azonosítót és nevet ad vissza. A listák a legújabb konfigurációkkal kezdődnek. Az egyedi lekérés nem találatkor `404` választ ad, a sikeres létrehozás mindkét esetben `201 Created`.

### 14.6 RuleEngine: bemenet és kiértékelési folyamat

A `RuleEngine` statikus segédosztály, tehát példányosítás vagy DI-regisztráció nélkül hívható. A jelenlegi kód alapján a működése a következő:

1. A `StructuredCaseState` egy szótár, amely a mezőneveket kis- és nagybetűtől függetlenül kezeli. A motor a feltétel `field` mezőjét pontos kulcsként keresi; pontozott/nested mezőútvonal feloldása nincs implementálva. Hiányzó kulcs értéke `null`.
2. A `Condition` tartalmazza a mezőt, az operátort és az elvárt értéket. A JSON-ból érkező `JsonElement` értékeket a motor .NET skalár típusokra alakítja: szöveg, egész/decimal/double szám, logikai érték vagy `null`.
3. Az operátor neve kis- és nagybetűtől független, és a környező whitespace le van vágva. Támogatott: `equals`, `notequals`, `in`, `notin`, `greaterthan`, `greaterthanorequal`, `lessthan`, `lessthanorequal`. Ismeretlen operátor eredménye `false`.
4. Az `equals` először a numerikus típusokat numerikusan hasonlítja össze (így például az egész `31` és az azonos értékű decimális szám egyezhet); más esetben a .NET `Equals` használatos. A szövegek összehasonlítása ezért kis- és nagybetűérzékeny. Két `null` egyenlő, egy `null` és egy nem-null érték nem egyenlő.
5. Az `in` akkor igaz, ha a tényleges érték egyezik az elvárt tömb/lista valamely elemével. A `notin` ennek a tagadása. Ha a `value` nem tömb vagy nem string lista/kollekció, az elvárt lista üres; emiatt az `in` hamis, a `notin` igaz.
6. A négy sorrendi operátor kizárólag numerikus értékekkel működik. Nem numerikus vagy hiányzó/null érték esetén az összehasonlítás hamis, nem történik szöveges rendezés vagy automatikus szám-parszolás.
7. Egy feltételcsoportnál az `all` lista minden feltételének, az `any` lista legalább egy feltételének kell igaznak lennie. Ha mindkét lista meg van adva, a két eredmény ÉS kapcsolatban van. Üres `all` lista igaz, üres `any` lista hamis; mindkét mező hiányában a csoport igaznak számít.
8. A `Rule.Conditions` nélküli szabály feltétel nélkül egyezőnek minősül. Az `EvaluateRules` minden szabályhoz létrehoz egy kiértékelési eredményt, a `GetMatchedRules` kiszűri az egyezőket, a `SelectRule` pedig a legnagyobb `Priority` értékű egyezőt választja ki. Egyezés hiányában `SelectedRule` értéke null. Azonos prioritás esetén az elsőként megadott egyező marad kiválasztva.

Az `Evaluate` eredménye három részt tartalmaz: az összes kiértékelést, az egyező szabályokat és a kiválasztott szabályt. A motor nem számol díjat, nem értelmezi a szabály eredményét, nem ad indoklást és nem ment adatbázisba.

### 14.7 A Rules.JSON és a RuleEngine közötti határ

A projektben található `Rules.JSON` szabálykészlet-verziót, feltételeket, továbbá `result`, `source` és `effectiveFrom` mezőket is tartalmaz. A RuleEngine `Rule` modellje jelenleg csak az `id`, `name`, `priority` és `conditions` tulajdonságokat definiálja. A JSON-ban lévő szabályok nem tartalmaznak `priority` mezőt, a JSON pedig egy szabálykészletet tartalmazó tömb/wrapper alakú. Emiatt a fájl közvetlen betöltése és az üzleti eredmények (például díjtételek) feldolgozása nincs a jelenlegi RuleEngine funkcionalitásában megvalósítva.

A `TextFileService` DI-regisztrálva van, de a jelenlegi kontrollerek és service-ek nem hívják. A beállított promptkönyvtárból a legfelső szintű `.txt` fájlokat fájlnév szerint rendezve olvassa össze; a szabálykönyvtárból kizárólag a `Rules.JSON` fájlt olvassa szövegként. Ez a szolgáltatás jelenleg nem kapcsolja össze a fájl tartalmát a `RuleEngine`-nel vagy a Processing-folyamattal. A könyvtárbeállítások környezetfüggő fájlrendszer-útvonalak, hiányzó könyvtár vagy fájl esetén kivételt dob.

### 14.8 Tárolt Processing-adatmodell

Az `AppDbContext` a Conversation, Message, Processing, Configuration és a későbbi feldolgozási lépésekhez szükséges entitások DbSet-jeit tartalmazza. A fő kapcsolatok:

- Egy Conversation több Message-t és Processinget foghat össze.
- Egy Processing egy `InputMessage`-re és egy Configurationre hivatkozik; emellett több, a feldolgozáshoz kötött Message tartozhat hozzá.
- Egy Processinghez legfeljebb egy PreProcessing, Rules, ResponsePlan és Response rekord tartozik.
- Egy Rules rekordhoz több MatchedRules rekord kapcsolódhat.
- Egy Processinghez több EvaluationResult rekord tartozhat.

A modellek a későbbi lépések eredményeit is leírják: a PreProcessing tisztított szöveget és strukturált állapot JSON-t tárolhat; a Rules döntést, indoklást és szabálykészlet-verziót; a ResponsePlan döntést, kötelező elemeket, válaszstruktúrát és forrásokat; a Response a generált szöveget, generálási módot, modell-/prompt-/sablonadatokat és futási időt; az EvaluationResult pedig metrikát, pontszámot, mértékegységet és opcionális megjegyzést. Ezek az entitások adatmodellként léteznek, de a jelenlegi API-folyamat nem tölti fel vagy kérdezi le őket.

Az EF Core kapcsolatoknál törlés esetén `NoAction` van konfigurálva. Ez azt jelenti, hogy a kapcsolódó rekordok adatbázisbeli törlését nem végzi automatikusan kaszkád módon az EF konfigurációja.

### 14.9 Endpointok és aktuális korlátaik

| Művelet | Jelenlegi eredmény |
|---|---|
| `GET /api/conversations` | Beszélgetések listája, létrehozási idő szerint csökkenő sorrendben. |
| `POST /api/Conversation/conversation` | Beszélgetés létrehozása; siker: `201`, érvénytelen DTO: `400`. |
| `GET /api/Conversation/conversation/{id}` | Beszélgetés lekérése; hiány esetén `404`. |
| `POST /api/conversations/{conversationId}/messages` | Felhasználói üzenet és `Created` Processing létrehozása; hibás tartalom: `400`, hiányzó conversation/configuration: `404`. |
| `GET /api/configurations` | Konfiguráció-összefoglalók listája. |
| `GET /api/configurations/{id}` | Részletes konfiguráció; hiány esetén `404`. |
| `POST /api/configurations/template` | Template konfiguráció létrehozása; siker: `201`. |
| `POST /api/configurations/llm` | LLM konfigurációs rekord létrehozása; siker: `201`. |
| `GET /api/Processing/GetProcessingResult/{processingId}` | Jelenleg üres `200 OK` válasz; a `processingId` nem kerül feldolgozásra, adatbázis-lekérdezés sincs. |

Az utolsó útvonal a controller nevéből és route attribútumából származik; a route kis-/nagybetűzése az ASP.NET Core útvonal-illesztésében nem jelent eltérő végpontot.

### 14.10 Rövid összegzés a tényleges készültségről

A jelenlegi backend valóban képes konfigurációkat és beszélgetéseket tárolni, illetve egy üzenethez konfigurációhoz kötött, kezdeti Processing rekordot létrehozni. Van önálló, egységnyi feltételek és prioritások kiértékelésére alkalmas RuleEngine. Ugyanakkor a feldolgozás lépései még nincsenek összefűzve: nincs prompt- vagy szabályfájl-betöltés a folyamatban, nincs előfeldolgozás, nincs adatbázisba mentett szabályeredmény, választerv vagy generált válasz, és a processing-result végpont sem ad tényleges eredményt. A konfiguráció LLM módja jelenleg csak konfigurációs adatok tárolását jelenti, nem LLM-hívást.
