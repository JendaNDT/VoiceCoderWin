using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CodePlanner.Core
{
    /// <summary>
    /// Model nastavení pro Gemini API, ukládaný v uživatelském profilu.
    /// </summary>
    public class GeminiNastaveni
    {
        public string GeminiApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-2.5-flash";
        public List<string> NedavneProjekty { get; set; } = new List<string>();

        public void PridejNedavnyProjekt(string cesta)
        {
            if (string.IsNullOrWhiteSpace(cesta)) return;

            if (NedavneProjekty == null)
            {
                NedavneProjekty = new List<string>();
            }

            NedavneProjekty.RemoveAll(x => string.Equals(x, cesta, StringComparison.OrdinalIgnoreCase));
            NedavneProjekty.Insert(0, cesta);

            if (NedavneProjekty.Count > 5)
            {
                NedavneProjekty.RemoveRange(5, NedavneProjekty.Count - 5);
            }

            Uloz();
        }

        public void OdeberNedavnyProjekt(string cesta)
        {
            if (string.IsNullOrWhiteSpace(cesta) || NedavneProjekty == null) return;
            NedavneProjekty.RemoveAll(x => string.Equals(x, cesta, StringComparison.OrdinalIgnoreCase));
            Uloz();
        }

        private static string ZiskejCestu()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "CodePlanner", "settings.json");
        }

        public static GeminiNastaveni Nacti()
        {
            try
            {
                string cesta = ZiskejCestu();
                if (File.Exists(cesta))
                {
                    string json = File.ReadAllText(cesta);
                    var nastaveni = JsonSerializer.Deserialize<GeminiNastaveni>(json);
                    if (nastaveni != null) return nastaveni;
                }
            }
            catch
            {
                // V případě chyby vrátíme výchozí nastavení
            }

            return new GeminiNastaveni();
        }

        public void Uloz()
        {
            try
            {
                string cesta = ZiskejCestu();
                string slozka = Path.GetDirectoryName(cesta);
                if (!Directory.Exists(slozka))
                {
                    Directory.CreateDirectory(slozka);
                }

                var opt = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, opt);
                File.WriteAllText(cesta, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("Nepodařilo se uložit nastavení: " + ex.Message, ex);
            }
        }

        [JsonIgnore]
        public string EfektivniApiKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(GeminiApiKey)) return GeminiApiKey.Trim();
                string envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                return envKey != null ? envKey.Trim() : "";
            }
        }
    }

    public class GeminiAnalizaVysledek
    {
        [JsonPropertyName("nazev")]
        public string Nazev { get; set; } = "";

        [JsonPropertyName("odpovedi")]
        public List<GeminiOdpoved> Odpovedi { get; set; } = new List<GeminiOdpoved>();
    }

    /// <summary>
    /// Jedna navržená odpověď z Gemini API.
    /// </summary>
    public class GeminiOdpoved
    {
        [JsonPropertyName("otazkaId")]
        public string OtazkaId { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("jePredpoklad")]
        public bool JePredpoklad { get; set; }
    }

    public class GeminiDynamickyVysledek
    {
        [JsonPropertyName("nazev")]
        public string Nazev { get; set; } = "";

        [JsonPropertyName("otazky")]
        public List<GeminiDynamickaOtazka> Otazky { get; set; } = new List<GeminiDynamickaOtazka>();
    }

    public class GeminiDynamickaOtazka
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("sekce")]
        public string Sekce { get; set; } = "";

        [JsonPropertyName("dopad")]
        public string Dopad { get; set; } = "Stredni"; // Vysoky / Stredni

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("napoveda")]
        public string Napoveda { get; set; } = "";

        [JsonPropertyName("vychoziPredpoklad")]
        public string VychoziPredpoklad { get; set; } = "";

        [JsonPropertyName("odpoved")]
        public string Odpoved { get; set; } = "";

        [JsonPropertyName("jePredpoklad")]
        public bool JePredpoklad { get; set; }

        [JsonPropertyName("moznosti")]
        public List<string> Moznosti { get; set; } = new List<string>();
    }

    public class GeminiKonzistenceVysledek
    {
        [JsonPropertyName("nalezy")]
        public List<GeminiKonzistenceNalez> Nalezy { get; set; } = new List<GeminiKonzistenceNalez>();
    }

    public class GeminiKonzistenceNalez
    {
        [JsonPropertyName("zavaznost")]
        public string Zavaznost { get; set; } = "Varovani";

        [JsonPropertyName("titulek")]
        public string Titulek { get; set; } = "";

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = "";
    }

    public class GeminiUserStoriesVysledek
    {
        [JsonPropertyName("stories")]
        public List<GeminiUserStory> Stories { get; set; } = new List<GeminiUserStory>();
    }

    public class GeminiUserStory
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("titulek")]
        public string Titulek { get; set; } = "";

        [JsonPropertyName("popis")]
        public string Popis { get; set; } = "";

        [JsonPropertyName("kriteria")]
        public List<string> Kriteria { get; set; } = new List<string>();

        [JsonPropertyName("priorita")]
        public string Priorita { get; set; } = "Střední";
    }

    /// <summary>
    /// Služba zajišťující volání Gemini API pro strukturovanou analýzu.
    /// </summary>
    public static class GeminiService
    {
        private static readonly HttpClient Client;

        static GeminiService()
        {
            Client = new HttpClient();
            Client.Timeout = TimeSpan.FromSeconds(90);
        }

        private static string CleanJson(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return "{}";
            string clean = rawJson.Trim();
            if (clean.StartsWith("```json")) clean = clean.Substring(7);
            else if (clean.StartsWith("```")) clean = clean.Substring(3);
            if (clean.EndsWith("```")) clean = clean.Substring(0, clean.Length - 3);
            return clean.Trim();
        }

        /// <summary>Ořízne příliš dlouhý text pro prompt na daný počet znaků a doplní poznámku o zkrácení.</summary>
        public static string OrezText(string text, int maxZnaku = 100_000)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxZnaku) return text;
            return text.Substring(0, maxZnaku) + Environment.NewLine + "[…zkráceno]";
        }

        /// <summary>Cesta k souboru s poslední surovou AI odpovědí – ukládá se sem při chybě parsování pro diagnostiku.</summary>
        public static string CestaPosledniAiOdpovedi
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodePlanner", "posledni_ai_odpoved.txt");

        private static void UlozSurovouAiOdpoved(string surovaOdpoved)
        {
            try
            {
                string cesta = CestaPosledniAiOdpovedi;
                string slozka = Path.GetDirectoryName(cesta);
                if (!string.IsNullOrEmpty(slozka)) Directory.CreateDirectory(slozka);
                File.WriteAllText(cesta, surovaOdpoved ?? "", Encoding.UTF8);
            }
            catch
            {
                // Diagnostický zápis nesmí shodit hlavní operaci.
            }
        }

        private static readonly JsonSerializerOptions AiJsonOpt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        /// <summary>Deserializace JSON odpovědi od AI. Při nevalidním JSONu uloží surovou odpověď
        /// do %AppData%\CodePlanner\posledni_ai_odpoved.txt a vyhodí srozumitelnou výjimku (inner = JsonException).</summary>
        private static T DeserializujAiOdpoved<T>(string surovaOdpoved) where T : class
        {
            string cleanText = CleanJson(surovaOdpoved);
            T vysledek;
            try
            {
                vysledek = JsonSerializer.Deserialize<T>(cleanText, AiJsonOpt);
            }
            catch (JsonException ex)
            {
                UlozSurovouAiOdpoved(surovaOdpoved);
                throw new Exception("AI vrátila odpověď v neočekávaném formátu. Zkuste akci zopakovat.", ex);
            }

            if (vysledek == null)
            {
                UlozSurovouAiOdpoved(surovaOdpoved);
                throw new Exception("AI vrátila odpověď v neočekávaném formátu. Zkuste akci zopakovat.");
            }

            return vysledek;
        }

        public static async Task TestPripojeniAsync(string apiKey, string model)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč nesmí být prázdný.");

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = "Say 'OK' and nothing else." }
                        }
                    }
                }
            };

            await PosliGeminiRequestAsync(apiKey, model, requestBody, default);
        }

        /// <summary>Interní výjimka nesoucí HTTP status kód – slouží k rozhodnutí, zda má smysl pokus opakovat.</summary>
        private sealed class GeminiApiException : Exception
        {
            public int StatusCode { get; }

            public GeminiApiException(string message, int statusCode) : base(message)
            {
                StatusCode = statusCode;
            }
        }

        /// <summary>Pauzy mezi automatickými opakováními při dočasné chybě (max. 2 opakování: 2 s a 5 s).</summary>
        private static readonly TimeSpan[] PauzyMeziPokusy = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) };

        /// <summary>Dočasné chyby, u kterých má smysl automaticky opakovat: HTTP 429/500/502/503, timeout a síťová chyba.</summary>
        private static bool JeDocasnaChyba(Exception ex)
        {
            if (ex is GeminiApiException api)
                return api.StatusCode == 429 || api.StatusCode == 500 || api.StatusCode == 502 || api.StatusCode == 503;
            return ex is TimeoutException || ex is HttpRequestException;
        }

        private static async Task<string> PosliGeminiRequestAsync(string apiKey, string model, object requestBody, CancellationToken cancellationToken)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            string requestJson = JsonSerializer.Serialize(requestBody);

            for (int pokus = 0; ; pokus++)
            {
                try
                {
                    return await PosliGeminiRequestJednouAsync(url, apiKey, requestJson, cancellationToken);
                }
                catch (Exception ex) when (pokus < PauzyMeziPokusy.Length && JeDocasnaChyba(ex) && !cancellationToken.IsCancellationRequested)
                {
                    // Dočasná chyba – počkáme a zkusíme to znovu. Task.Delay s tokenem se při zrušení okamžitě ukončí.
                    await Task.Delay(PauzyMeziPokusy[pokus], cancellationToken);
                }
            }
        }

        private static async Task<string> PosliGeminiRequestJednouAsync(string url, string apiKey, string requestJson, CancellationToken cancellationToken)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Add("x-goog-api-key", apiKey);
            requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            try
            {
                var response = await Client.SendAsync(requestMessage, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = "";
                    try { errContent = await response.Content.ReadAsStringAsync(); } catch { }
                    
                    string friendlyMsg = null;
                    if (!string.IsNullOrEmpty(errContent))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(errContent);
                            if (doc.RootElement.TryGetProperty("error", out var errorProp))
                            {
                                if (errorProp.TryGetProperty("message", out var msgProp))
                                {
                                    string origMsg = msgProp.GetString();
                                    if (origMsg != null)
                                    {
                                        if (origMsg.Contains("API key not valid") || origMsg.Contains("API_KEY_INVALID") || origMsg.Contains("key is invalid"))
                                        {
                                            friendlyMsg = "Zadaný API klíč je neplatný. Ověřte prosím správnost klíče v Nastavení AI.";
                                        }
                                        else if (origMsg.Contains("Quota exceeded") || origMsg.Contains("RESOURCE_EXHAUSTED") || origMsg.Contains("429"))
                                        {
                                            friendlyMsg = "Byl překročen limit požadavků (Quota Exceeded / Rate Limit). Zkuste to prosím znovu za minutu.";
                                        }
                                        else
                                        {
                                            friendlyMsg = $"Chyba API: {origMsg}";
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (friendlyMsg == null)
                    {
                        friendlyMsg = $"Gemini API vrátilo chybu {(int)response.StatusCode} ({response.ReasonPhrase}).";
                        if (!string.IsNullOrEmpty(errContent)) friendlyMsg += $" Detail: {errContent}";
                    }
                    throw new GeminiApiException(friendlyMsg, (int)response.StatusCode);
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                return ZiskejTextZCandidate(responseJson);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Požadavek na Gemini API vypršel. Zkontrolujte prosím připojení k internetu a stav Gemini služby.", ex);
            }
        }

        private static string ZiskejTextZCandidate(string responseJson)
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                throw new Exception("V odpovědi Gemini API chybí kandidáti (candidates).");

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) || 
                !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                throw new Exception("V odpovědi Gemini API chybí obsah kandidáta (content.parts).");

            var firstPart = parts[0];
            if (!firstPart.TryGetProperty("text", out var textProp))
                throw new Exception("Odpověď z Gemini API neobsahuje textové pole.");

            return textProp.GetString() ?? "";
        }

        public static string SestavPrompt(string napad, string typProjektuKlic, string referencniText = null, bool maMockup = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Jsi expert na softwarovou analýzu a tvorbu zadání pro kódovací agenty.");
            sb.AppendLine($"Tvým úkolem je analyzovat původní nápad uživatele na aplikaci typu \"{SpecSluzba.VratNazevTypu(typProjektuKlic)}\" a navrhnout:");
            sb.AppendLine("1. Krátký, výstižný název projektu (maximálně 5 slov).");
            sb.AppendLine("2. Seznam 7 až 10 doplňujících specifikačních otázek, které jsou ŠITÉ NA MÍRU tomuto konkrétnímu projektu a technické doméně.");
            sb.AppendLine();
            sb.AppendLine("Pravidla pro otázky:");
            sb.AppendLine("- Každá otázka must patřit do jedné z těchto sekcí: Cíl a uživatelé, Rozsah, UX, Data, Technika, Akceptace, Rizika.");
            sb.AppendLine("- Otázky musí být konkrétní (např. u fitness aplikace se ptej na senzory/API, u e-shopu na platby, ne obecně).");
            sb.AppendLine("- Každá otázka musí mít stanovený dopad (Vysoky pro architekturu/cenu/bezpečnost, Stredni pro UX/detaily).");
            sb.AppendLine("- Pokud je to možné, použij pro příslušné otázky následující standardní identifikátory (IDs):");
            sb.AppendLine("  * `cil-problem` (pro smysl/problém projektu)");
            sb.AppendLine("  * `cil-uzivatele` (pro role/uživatele)");
            sb.AppendLine("  * `tech-platforma` (pro platformu/jazyky)");
            sb.AppendLine("  * `tech-offline` (pro offline/online režim)");
            sb.AppendLine("  * `data-obsah` (pro ukládaná data/databázi)");
            sb.AppendLine("  * `data-export` (pro export/tisk)");
            sb.AppendLine("  * `rozsah-nongoals` (pro to, co se dělat nebude)");
            sb.AppendLine("  * `akceptace` (pro kritéria dokončení)");
            sb.AppendLine("  * `ux-obrazovky` (pro vzhled/obrazovky)");
            sb.AppendLine("  * `rizika` (pro rizika a nejasnosti)");
            sb.AppendLine("- Pokud projekt vyžaduje specifickou otázku mimo tyto standardní, vymysli pro ni nový výstižný identifikátor.");
            sb.AppendLine("- Pro každou otázku navrhni:");
            sb.AppendLine("  a) Nápovědu pro uživatele (jak o otázce přemýšlet).");
            sb.AppendLine("  b) Výchozí doporučený předpoklad (default assumption), který použijeme, pokud uživatel nebude vědět.");
            sb.AppendLine("  c) Odpověď šitou na míru: Pokud z původního nápadu uživatele vyplývá odpověď, formuluj ji a nastav \"jePredpoklad\": false. Pokud informace v nápadu chybí, vlož text výchozího předpokladu a nastav \"jePredpoklad\": true.");
            sb.AppendLine("  d) Přesně 3 krátké, konkrétní typické možnosti rychlé odpovědi (pole stringů `moznosti`), které uživatel může vybrat (např. [\"SQLite local DB\", \"PostgreSQL v cloudu\", \"Ukládání do JSON souborů\"]).");
            sb.AppendLine();
            sb.AppendLine("Zde jsou základní standardní otázky a jejich výchozí texty pro inspiraci:");

            foreach (var ot in Otazky.Vse)
            {
                sb.AppendLine($"- Výchozí ID: {ot.Id}");
                sb.AppendLine($"  Výchozí sekce: {ot.Sekce}");
                sb.AppendLine($"  Výchozí otázka: {ot.GetText(typProjektuKlic)}");
                sb.AppendLine($"  Výchozí předpoklad: {ot.GetVychoziPredpoklad(typProjektuKlic)}");
            }

            sb.AppendLine();
            sb.AppendLine("Odpověz výhradně ve formátu JSON podle tohoto schématu. Nevracej žádný jiný text, pouze tento JSON. Nepoužívej markdown obal typu ```json a ```.");
            sb.AppendLine("{");
            sb.AppendLine("  \"nazev\": \"Navržený název projektu\",");
            sb.AppendLine("  \"otazky\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"id\": \"identifikator-otazky\",");
            sb.AppendLine("      \"sekce\": \"Název sekce\",");
            sb.AppendLine("      \"dopad\": \"Vysoky\" nebo \"Stredni\",");
            sb.AppendLine("      \"text\": \"Znění otázky na míru\",");
            sb.AppendLine("      \"napoveda\": \"Nápověda k otázce na míru\",");
            sb.AppendLine("      \"vychoziPredpoklad\": \"Výchozí předpoklad na míru\",");
            sb.AppendLine("      \"moznosti\": [\"rychlá volba 1\", \"rychlá volba 2\", \"rychlá volba 3\"],");
            sb.AppendLine("      \"odpoved\": \"Navržená odpověď nebo text výchozího předpokladu na míru\",");
            sb.AppendLine("      \"jePredpoklad\": true nebo false");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Zde je původní nápad uživatele:");
            sb.AppendLine(napad);

            if (!string.IsNullOrWhiteSpace(referencniText))
            {
                sb.AppendLine();
                sb.AppendLine("Uživatel přiložil také následující referenční podklady k projektu. Použij je k přesnějšímu nastavení otázek a odpovědí:");
                sb.AppendLine(OrezText(referencniText));
            }

            if (maMockup)
            {
                sb.AppendLine();
                sb.AppendLine("Uživatel přiložil také nákres / screenshot rozhraní (mockup) jako obrázek. Prozkoumej tento obrázek a využij ho k přesnějšímu nastavení otázek a odpovědí, aby odpovídaly vizuálnímu návrhu rozhraní.");
            }

            return sb.ToString();
        }

        public static async Task<GeminiDynamickyVysledek> AnalyzujNapadAsync(string apiKey, string model, string napad, string typProjektuKlic, string referencniText = null, string mockupBase64 = null, string mockupMime = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            if (string.IsNullOrWhiteSpace(napad))
                throw new ArgumentException("Nápad k analýze nesmí být prázdný.");

            var partsList = new List<object>
            {
                new { text = SestavPrompt(napad, typProjektuKlic, referencniText, !string.IsNullOrWhiteSpace(mockupBase64)) }
            };

            if (!string.IsNullOrWhiteSpace(mockupBase64))
            {
                partsList.Add(new
                {
                    inlineData = new
                    {
                        mimeType = mockupMime ?? "image/png",
                        data = mockupBase64
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = partsList.ToArray()
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            if (string.IsNullOrWhiteSpace(textResponse))
                throw new Exception("Odpověď z Gemini API je prázdná.");

            return DeserializujAiOdpoved<GeminiDynamickyVysledek>(textResponse);
        }

        public static async Task<string> PrepisAudioAsync(string apiKey, string model, string cestaWav, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            if (!File.Exists(cestaWav))
                throw new FileNotFoundException("Zvukový soubor nebyl nalezen.", cestaWav);

            byte[] audioBytes = File.ReadAllBytes(cestaWav);
            string base64Audio = Convert.ToBase64String(audioBytes);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = "audio/wav",
                                    data = base64Audio
                                }
                            },
                            new
                            {
                                text = "Přepiš toto audio slovo od slova do češtiny (případně do jazyka, kterým se mluví). Vypiš POUZE a VÝHRADNĚ výsledný přepis textu bez jakýchkoliv úvodních či vysvětlujících frází, bez uvozovek a bez komentářů. Pokud je v audiu ticho nebo šum, nevypisuj vůbec nic."
                            }
                        }
                    }
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            return textResponse?.Trim() ?? "";
        }

        public static async Task<List<Nalez>> AnalyzujKonzistenciAsync(string apiKey, string model, SpecProjekt projekt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            var sb = new StringBuilder();
            sb.AppendLine("Jsi expert na softwarovou architekturu a analýzu požadavků.");
            sb.AppendLine("Tvým úkolem je analyzovat následující specifikaci projektu a najít jakékoliv logické rozpory, technické nesrovnalosti, bezpečnostní mezery nebo chybějící vazby mezi požadavky a navrženými odpověďmi.");
            sb.AppendLine();
            sb.AppendLine("Zaměř se na:");
            sb.AppendLine("1. Skutečné logické rozpory (např. v jedné otázce se tvrdí, že data budou jen lokálně, v jiné se mluví o synchronizaci přes server).");
            sb.AppendLine("2. Technické nesrovnalosti (např. databáze SQLite pro čistě webovou aplikaci bez serveru, nebo platby kartou bez zmínky o zabezpečení/SSL).");
            sb.AppendLine("3. Zásadní mezery (např. v uživatelích se definuje role 'Administrátor', ale v právech/bezpečnosti chybí přihlašování).");
            sb.AppendLine("4. Nerealistické nebo vágní plány.");
            sb.AppendLine();
            sb.AppendLine("Odpověz výhradně ve formátu JSON podle tohoto schématu. Nevracej žádný jiný text, pouze tento JSON. Nepoužívej markdown obal.");
            sb.AppendLine("{");
            sb.AppendLine("  \"nalezy\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"zavaznost\": \"Rozpor\" nebo \"Varovani\",");
            sb.AppendLine("      \"titulek\": \"Název problému\",");
            sb.AppendLine("      \"detail\": \"Podrobný popis rozporu nebo mezery a návrh řešení.\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Zde je kompletní specifikace projektu:");
            sb.AppendLine(OrezText(SpecSluzba.RenderMarkdown(projekt)));

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = sb.ToString() }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            var vysledek = DeserializujAiOdpoved<GeminiKonzistenceVysledek>(textResponse);

            var nalezy = new List<Nalez>();
            if (vysledek.Nalezy != null)
            {
                foreach (var n in vysledek.Nalezy)
                {
                    nalezy.Add(new Nalez
                    {
                        Zavaznost = string.Equals(n.Zavaznost, "Rozpor", StringComparison.OrdinalIgnoreCase) ? Zavaznost.Rozpor : Zavaznost.Varovani,
                        Titulek = n.Titulek ?? "",
                        Detail = n.Detail ?? ""
                    });
                }
            }

            return nalezy;
        }

        public static async Task<List<UserStory>> GenerujUserStoriesAsync(string apiKey, string model, SpecProjekt projekt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            var sb = new StringBuilder();
            sb.AppendLine("Jsi agilní kouč a softwarový analytik.");
            sb.AppendLine("Tvým úkolem je na základě níže uvedené specifikace projektu navrhnout sadu konkrétních, realizovatelných a srozumitelných User Stories pro vývojáře.");
            sb.AppendLine();
            sb.AppendLine("Pro každou User Story definuj:");
            sb.AppendLine("1. Jedinečný identifikátor (např. US-01, US-02). Generuj jich 8 až 15 podle rozsahu aplikace.");
            sb.AppendLine("2. Stručný, výstižný titulek.");
            sb.AppendLine("3. Popis ve standardním formátu: 'Jako [role] chci [funkce], abych [přínos].'");
            sb.AppendLine("4. Seznam 3 až 6 konkrétních a testovatelných Akceptačních kritérií (Acceptance Criteria).");
            sb.AppendLine("5. Prioritu: 'Vysoká', 'Střední', nebo 'Nízká'.");
            sb.AppendLine();
            sb.AppendLine("Odpověz výhradně ve formátu JSON podle tohoto schématu. Nevracej žádný jiný text, pouze tento JSON. Nepoužívej markdown obal.");
            sb.AppendLine("{");
            sb.AppendLine("  \"stories\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"id\": \"US-01\",");
            sb.AppendLine("      \"titulek\": \"Název user story\",");
            sb.AppendLine("      \"popis\": \"Jako... chci... abych...\",");
            sb.AppendLine("      \"kriteria\": [");
            sb.AppendLine("        \"kritérium 1\",");
            sb.AppendLine("        \"kritérium 2\"");
            sb.AppendLine("      ],");
            sb.AppendLine("      \"priorita\": \"Vysoká\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Zde je kompletní specifikace projektu:");
            sb.AppendLine(OrezText(SpecSluzba.RenderMarkdown(projekt)));

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = sb.ToString() }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            var vysledek = DeserializujAiOdpoved<GeminiUserStoriesVysledek>(textResponse);

            var stories = new List<UserStory>();
            if (vysledek.Stories != null)
            {
                foreach (var s in vysledek.Stories)
                {
                    stories.Add(new UserStory
                    {
                        Id = s.Id ?? "",
                        Titulek = s.Titulek ?? "",
                        Popis = s.Popis ?? "",
                        Kriteria = s.Kriteria ?? new List<string>(),
                        Priorita = s.Priorita ?? "Střední"
                    });
                }
            }

            return stories;
        }

        public static async Task<string> PosliChatZpravuAsync(string apiKey, string model, SpecProjekt projekt, List<ChatMessage> novyChatLog, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            string specMarkdown = OrezText(SpecSluzba.RenderMarkdown(projekt));
            string systemPrompt = "Jsi zkušený softwarový architekt, agilní kouč a seniorní vývojář. Odpovídáš na dotazy ohledně navrhovaného projektu.\n\n" +
                                 "Zde je kompletní specifikace projektu, která je tvým jediným zdrojem pravdy o cílech a parametrech systému. Všechny své odpovědi přizpůsob tomuto kontextu:\n\n" +
                                 specMarkdown + "\n\n" +
                                 "Odpovídej přímo, konstruktivně a srozumitelně v češtině. Pomáhej s architekturou, databázemi, návrhem rozhraní, kódem nebo testováním.";

            var vsechnyZpravy = novyChatLog ?? new List<ChatMessage>();

            // Posíláme jen posledních 20 zpráv historie – starší kontext drží specifikace v system promptu.
            var zpravy = vsechnyZpravy.Count > 20
                ? vsechnyZpravy.Skip(vsechnyZpravy.Count - 20).ToList()
                : vsechnyZpravy;

            // Mockup přikládáme jen k úplně první zprávě konverzace (historie před aktuální zprávou je prázdná),
            // aby se obrázek neposílal znovu při každém dalším volání.
            bool prilozitMockup = vsechnyZpravy.Count == 1 && !string.IsNullOrWhiteSpace(projekt.MockupBase64);

            var turnsList = new List<object>();
            for (int i = 0; i < zpravy.Count; i++)
            {
                var msg = zpravy[i];
                var parts = new List<object>
                {
                    new { text = msg.Text }
                };

                if (prilozitMockup && i == 0 && string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    string mime = (projekt.MockupNazev != null && (projekt.MockupNazev.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || projekt.MockupNazev.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))) ? "image/jpeg" : "image/png";
                    parts.Add(new
                    {
                        inlineData = new
                        {
                            mimeType = mime,
                            data = projekt.MockupBase64
                        }
                    });
                }

                turnsList.Add(new
                {
                    role = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase) ? "user" : "model",
                    parts = parts.ToArray()
                });
            }

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                },
                contents = turnsList.ToArray()
            };

            return await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
        }

        public static async Task<ProjektMetriky> GenerujMetrikyAsync(string apiKey, string model, SpecProjekt projekt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API klíč pro Gemini nesmí být prázdný.");

            var sb = new StringBuilder();
            sb.AppendLine("Jsi seniorní projektový manažer, IT architekt a odhadce softwarových projektů.");
            sb.AppendLine("Tvým úkolem je na základě kompletní specifikace a parametrů projektu provést odhad pracnosti, navrhnout složení týmu, doporučit rozpočet a vypracovat technický rozbor.");
            sb.AppendLine();
            sb.AppendLine("Odpověz výhradně ve formátu JSON podle následujícího schématu. Nevracej žádný jiný doprovodný text, pouze tento JSON. Nepoužívej markdown obal typu ```json a ```.");
            sb.AppendLine("{");
            sb.AppendLine("  \"casovyOdhadMin\": \"Např. 80 hodin\",");
            sb.AppendLine("  \"casovyOdhadMax\": \"Např. 120 hodin\",");
            sb.AppendLine("  \"komplexita\": \"Nízká\", \"Střední\" nebo \"Vysoká\",");
            sb.AppendLine("  \"slozeniTymu\": \"Doporučené složení vývojářů a testerů\",");
            sb.AppendLine("  \"doporucenyRozpocet\": \"Např. 100 000 - 150 000 Kč\",");
            sb.AppendLine("  \"technickyRozbor\": \"Stručné odrážky s doporučením pro architekturu, technologie a databáze\",");
            sb.AppendLine("  \"rizikaMetriky\": [\"riziko 1\", \"riziko 2\", \"riziko 3\"]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Zde je aktuální specifikace projektu:");
            sb.AppendLine(OrezText(SpecSluzba.RenderMarkdown(projekt)));

            if (projekt.UserStories != null && projekt.UserStories.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Zde je seznam agilních User Stories pro přesnější odhad:");
                foreach (var us in projekt.UserStories)
                {
                    sb.AppendLine($"- {us.Id}: {us.Titulek} (Priorita: {us.Priorita})");
                }
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = sb.ToString() }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string textResponse = await PosliGeminiRequestAsync(apiKey, model, requestBody, cancellationToken);
            var vysledek = DeserializujAiOdpoved<GeminiMetrikyVysledek>(textResponse);

            return new ProjektMetriky
            {
                CasovyOdhadMin = vysledek.CasovyOdhadMin ?? "",
                CasovyOdhadMax = vysledek.CasovyOdhadMax ?? "",
                Komplexita = vysledek.Komplexita ?? "Střední",
                SlozeniTymu = vysledek.SlozeniTymu ?? "",
                DoporucenyRozpocet = vysledek.DoporucenyRozpocet ?? "",
                TechnickyRozbor = vysledek.TechnickyRozbor ?? "",
                RizikaMetriky = vysledek.RizikaMetriky ?? new List<string>(),
                CasVypoctu = DateTime.Now
            };
        }
    }


    public class GeminiMetrikyVysledek
    {
        [JsonPropertyName("casovyOdhadMin")]
        public string CasovyOdhadMin { get; set; } = "";

        [JsonPropertyName("casovyOdhadMax")]
        public string CasovyOdhadMax { get; set; } = "";

        [JsonPropertyName("komplexita")]
        public string Komplexita { get; set; } = "";

        [JsonPropertyName("slozeniTymu")]
        public string SlozeniTymu { get; set; } = "";

        [JsonPropertyName("doporucenyRozpocet")]
        public string DoporucenyRozpocet { get; set; } = "";

        [JsonPropertyName("technickyRozbor")]
        public string TechnickyRozbor { get; set; } = "";

        [JsonPropertyName("rizikaMetriky")]
        public List<string> RizikaMetriky { get; set; } = new List<string>();
    }
}
// konec souboru
