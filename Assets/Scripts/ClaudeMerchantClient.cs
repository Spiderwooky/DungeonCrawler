using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Réponse structurée d'un tour de dialogue marchand, après parsing de la sortie JSON de Claude.</summary>
public class MerchantReply
{
    public bool Success;
    public string ErrorMessage;

    public string Dialogue = "";
    public string Action = "none";       // "none" | "confirm_deal" | "farewell"
    public string Direction = "none";    // "none" | "player_buys" | "player_sells"
    public string ItemId = "";
    public int Quantity;
    public int Price;
    public int MoodDelta;
    public int ApprovalDelta;
}

/// <summary>
/// Client HTTP minimal vers l'API Messages de Claude, dédié au dialogue marchand.
/// Un appel = un tour de dialogue : pas de streaming, pas d'outils — la réponse est
/// contrainte au format JSON défini par BuildSchema() via output_config.format
/// (sorties structurées, GA, aucun header beta requis).
/// </summary>
public class ClaudeMerchantClient
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string Model = "claude-haiku-4-5";
    private const int MaxTokens = 500;
    private const int TimeoutSeconds = 15;

    public async Awaitable<MerchantReply> SendAsync(
        string apiKey,
        string staticSystemPrompt,
        string dynamicContext,
        List<(string role, string content)> history,
        string playerMessage)
    {
        JObject body = BuildRequestBody(staticSystemPrompt, dynamicContext, history, playerMessage);
        byte[] payload = Encoding.UTF8.GetBytes(body.ToString());

        using var request = new UnityWebRequest(Endpoint, "POST")
        {
            uploadHandler = new UploadHandlerRaw(payload),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = TimeoutSeconds
        };
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-api-key", apiKey);
        request.SetRequestHeader("anthropic-version", AnthropicVersion);

        var completion = new AwaitableCompletionSource<MerchantReply>();
        UnityWebRequestAsyncOperation op = request.SendWebRequest();
        op.completed += _ => completion.SetResult(ParseResult(request));

        return await completion.Awaitable;
    }

    private MerchantReply ParseResult(UnityWebRequest request)
    {
        if (request.result != UnityWebRequest.Result.Success)
        {
            return new MerchantReply
            {
                Success = false,
                ErrorMessage = $"{request.result} ({request.responseCode}) : {request.error}"
            };
        }

        try
        {
            JObject response = JObject.Parse(request.downloadHandler.text);

            if (response["type"]?.ToString() == "error")
            {
                string apiMessage = response["error"]?["message"]?.ToString() ?? "Erreur API inconnue.";
                return new MerchantReply { Success = false, ErrorMessage = apiMessage };
            }

            if (response["stop_reason"]?.ToString() == "refusal")
                return new MerchantReply { Success = false, ErrorMessage = "Requête refusée par les filtres de sécurité." };

            string text = ExtractFirstTextBlock(response["content"] as JArray);
            if (string.IsNullOrEmpty(text))
                return new MerchantReply { Success = false, ErrorMessage = "Réponse vide." };

            JObject structured = JObject.Parse(text);
            return new MerchantReply
            {
                Success = true,
                Dialogue = structured.Value<string>("dialogue") ?? "",
                Action = structured.Value<string>("action") ?? "none",
                Direction = structured.Value<string>("direction") ?? "none",
                ItemId = structured.Value<string>("item_id") ?? "",
                Quantity = structured.Value<int?>("quantity") ?? 0,
                Price = structured.Value<int?>("price") ?? 0,
                MoodDelta = Mathf.Clamp(structured.Value<int?>("mood_delta") ?? 0, -5, 5),
                ApprovalDelta = Mathf.Clamp(structured.Value<int?>("approval_delta") ?? 0, -5, 5),
            };
        }
        catch (Exception e)
        {
            return new MerchantReply { Success = false, ErrorMessage = $"Réponse illisible : {e.Message}" };
        }
    }

    private static string ExtractFirstTextBlock(JArray content)
    {
        if (content == null) return null;
        foreach (JToken block in content)
        {
            if (block["type"]?.ToString() == "text")
                return block.Value<string>("text");
        }
        return null;
    }

    private JObject BuildRequestBody(
        string staticSystemPrompt,
        string dynamicContext,
        List<(string role, string content)> history,
        string playerMessage)
    {
        var systemBlocks = new JArray
        {
            new JObject
            {
                ["type"] = "text",
                ["text"] = staticSystemPrompt,
                ["cache_control"] = new JObject { ["type"] = "ephemeral" }
            },
            new JObject
            {
                ["type"] = "text",
                ["text"] = dynamicContext
            }
        };

        var messages = new JArray();
        foreach ((string role, string content) in history)
            messages.Add(new JObject { ["role"] = role, ["content"] = content });
        messages.Add(new JObject { ["role"] = "user", ["content"] = playerMessage });

        return new JObject
        {
            ["model"] = Model,
            ["max_tokens"] = MaxTokens,
            ["system"] = systemBlocks,
            ["messages"] = messages,
            ["output_config"] = new JObject
            {
                ["format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["schema"] = BuildSchema()
                }
            }
        };
    }

    private static JObject BuildSchema()
    {
        return new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["dialogue"] = new JObject { ["type"] = "string" },
                ["action"] = new JObject { ["type"] = "string", ["enum"] = new JArray("none", "confirm_deal", "farewell") },
                ["direction"] = new JObject { ["type"] = "string", ["enum"] = new JArray("none", "player_buys", "player_sells") },
                ["item_id"] = new JObject { ["type"] = "string" },
                ["quantity"] = new JObject { ["type"] = "integer" },
                ["price"] = new JObject { ["type"] = "integer" },
                ["mood_delta"] = new JObject { ["type"] = "integer" },
                ["approval_delta"] = new JObject { ["type"] = "integer" }
            },
            ["required"] = new JArray(
                "dialogue", "action", "direction", "item_id", "quantity", "price", "mood_delta", "approval_delta"),
            ["additionalProperties"] = false
        };
    }
}
