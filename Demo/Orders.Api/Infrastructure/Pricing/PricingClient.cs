public interface IPricingClient
{
    Task<PriceResponse?> GetPriceAsync(string productId, int quantity, string? coupon, CancellationToken ct = default);
}

public sealed class PricingClient(IHttpClientFactory httpFactory) : IPricingClient
{
    public async Task<PriceResponse?> GetPriceAsync(string productId, int quantity, string? coupon, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("pricing");
        HttpResponseMessage res;
        try
        {
            res = await client.PostAsJsonAsync("/price", new { ProductId = productId, Quantity = quantity, Coupon = coupon }, ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        if ((int)res.StatusCode is >= 400 and < 500)
            throw new EngineErrorException((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));
        if (!res.IsSuccessStatusCode)
            return null;
        return await res.Content.ReadFromJsonAsync<PriceResponse>(cancellationToken: ct);
    }
}
