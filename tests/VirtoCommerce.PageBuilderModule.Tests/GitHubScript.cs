using System;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal sealed class GitHubScript(params (string Method, string Url, Func<HttpRequestMessage, string, HttpResponseMessage> Respond)[] script)
    : HttpMessageHandler, IHttpClientFactory
{
    private int _step;
    private ExceptionDispatchInfo _failed;

    public int Requests => _step;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            Assert.True(_step < script.Length, $"Unexpected request #{_step + 1}: {request.Method} {request.RequestUri}");
            var (method, url, respond) = script[_step++];
            Assert.Equal(method, request.Method.Method);
            Assert.Equal(url, request.RequestUri!.PathAndQuery.TrimStart('/'));
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request, body);
        }
        catch (Exception failure) when (failure is IAssertionException)
        {
            _failed ??= ExceptionDispatchInfo.Capture(failure);
            throw;
        }
    }

    public void AssertDone()
    {
        _failed?.Throw();
        Assert.Equal(script.Length, _step);
    }

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("https://api.github.test/") };

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("{}") };
}
