using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JewelryStore.Models;
using Microsoft.Extensions.Options;

namespace JewelryStore.Services;

public sealed record PayPalOrderResult(string Id, string ApprovalUrl);
public sealed record PayPalCaptureResult(bool IsCompleted, string TransactionId);

public sealed class PayPalPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly PayPalSettings _settings;

    public PayPalPaymentService(HttpClient httpClient, IOptions<PayPalSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ClientId)
        && !string.IsNullOrWhiteSpace(_settings.ClientSecret);

    public async Task<PayPalOrderResult> CreateOrderAsync(
        Order order,
        decimal amount,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var request = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = order.OrderNumber,
                    custom_id = order.Id.ToString(),
                    description = $"PrinciGold order {order.OrderNumber}",
                    amount = new
                    {
                        currency_code = _settings.Currency,
                        value = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    }
                }
            },
            application_context = new
            {
                brand_name = "PrinciGold",
                user_action = "PAY_NOW",
                return_url = returnUrl,
                cancel_url = cancelUrl
            }
        };

        using var httpRequest = CreateRequest(HttpMethod.Post, "/v2/checkout/orders", accessToken);
        httpRequest.Content = JsonContent.Create(request);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "PayPal order creation failed.", cancellationToken);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString() ?? throw new InvalidOperationException("PayPal did not return an order ID.");
        var approvalUrl = root.GetProperty("links").EnumerateArray()
            .FirstOrDefault(link => link.GetProperty("rel").GetString() == "approve")
            .GetProperty("href").GetString();

        return string.IsNullOrWhiteSpace(approvalUrl)
            ? throw new InvalidOperationException("PayPal did not return an approval URL.")
            : new PayPalOrderResult(id, approvalUrl);
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(string paypalOrderId, CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = CreateRequest(HttpMethod.Post, $"/v2/checkout/orders/{Uri.EscapeDataString(paypalOrderId)}/capture", accessToken);
        request.Content = JsonContent.Create(new { });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "PayPal payment capture failed.", cancellationToken);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        var transactionId = root.TryGetProperty("purchase_units", out var purchaseUnits)
            ? purchaseUnits.EnumerateArray()
                .SelectMany(unit => unit.TryGetProperty("payments", out var payments) && payments.TryGetProperty("captures", out var captures)
                    ? captures.EnumerateArray()
                    : Enumerable.Empty<JsonElement>())
                .Select(capture => capture.TryGetProperty("id", out var id) ? id.GetString() : null)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))
            : null;

        return new PayPalCaptureResult(
            root.TryGetProperty("status", out var status) && status.GetString() == "COMPLETED",
            transactionId ?? paypalOrderId);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("PayPal is not configured.");
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("/v1/oauth2/token"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "PayPal authentication failed.", cancellationToken);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("PayPal did not return an access token.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private Uri BuildUri(string path) => new($"{_settings.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}");

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string message, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"{message} HTTP {(int)response.StatusCode}: {detail}");
    }
}
