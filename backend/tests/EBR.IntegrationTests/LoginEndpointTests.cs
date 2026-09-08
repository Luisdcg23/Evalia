using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBR.IntegrationTests;

public sealed class LoginEndpointTests : IClassFixture<EbrApiFactory>
{
    private readonly HttpClient _client;

    public LoginEndpointTests(EbrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LoginReturnsAccessTokenForValidDevelopmentAdministrator()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@ebr.local",
            password = "EbrLocal2026!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal("ADMINISTRADOR", result.Role);
    }

    [Fact]
    public async Task RefreshRotatesTokenAndRejectsReuse()
    {
        using var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@ebr.local",
            password = "EbrLocal2026!"
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));

        using var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = login.RefreshToken
        });
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        using var reuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = login.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task AdministratorEndpointAllowsAdministratorAndForbidsCoordinator()
    {
        var administrator = await LoginAsync("admin@ebr.local");
        using var administratorRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/system-status");
        administratorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administrator.AccessToken);
        using var administratorResponse = await _client.SendAsync(administratorRequest);

        var coordinator = await LoginAsync("coordinador@ebr.local");
        using var coordinatorRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/system-status");
        coordinatorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coordinator.AccessToken);
        using var coordinatorResponse = await _client.SendAsync(coordinatorRequest);

        Assert.Equal(HttpStatusCode.OK, administratorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, coordinatorResponse.StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesTheRefreshToken()
    {
        var login = await LoginAsync("tecnico@ebr.local");
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = login.RefreshToken })
        };
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using var logoutResponse = await _client.SendAsync(logoutRequest);
        using var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = login.RefreshToken
        });

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task RegistrationRequiresAdministratorApprovalBeforeLogin()
    {
        var email = $"solicitante-{Guid.NewGuid():N}@example.local";
        using var registrationResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Solicitante de Prueba",
            documentNumber = "001-0000000-1",
            phoneNumber = "8095550101",
            email,
            password = "Registro2026!",
            requestedRole = "USUARIO_DELEGADO"
        });
        using var prematureLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Registro2026!"
        });

        Assert.Equal(HttpStatusCode.Accepted, registrationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, prematureLogin.StatusCode);

        var administrator = await LoginAsync("admin@ebr.local");
        using var pendingRequest = new HttpRequestMessage(HttpMethod.Get, "/api/users/pending");
        pendingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administrator.AccessToken);
        using var pendingResponse = await _client.SendAsync(pendingRequest);
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<PendingUserResponse>>();
        var user = Assert.Single(pending!, item => item.Email == email);

        using var approvalRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{user.Id}/approve");
        approvalRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administrator.AccessToken);
        using var approvalResponse = await _client.SendAsync(approvalRequest);
        using var approvedLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Registro2026!"
        });

        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, approvalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approvedLogin.StatusCode);
    }

    [Fact]
    public async Task CurrentUserReturnsAuthenticatedIdentityAndRole()
    {
        var coordinator = await LoginAsync("coordinador@ebr.local");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coordinator.AccessToken);

        using var response = await _client.SendAsync(request);
        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(currentUser);
        Assert.Equal("coordinador@ebr.local", currentUser.Email);
        Assert.Equal("COORDINADOR", currentUser.Role);
        Assert.Empty(currentUser.AuthorizedCompanyIds);
    }

    [Fact]
    public async Task PasswordRecoveryCodeCanBeUsedOnlyOnce()
    {
        var email = $"recuperacion-{Guid.NewGuid():N}@example.local";
        await RegisterAndApproveAsync(email, "Inicial2026!");
        using var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email
        });
        var recovery = await forgotResponse.Content.ReadFromJsonAsync<RecoveryResponse>();

        Assert.Equal(HttpStatusCode.Accepted, forgotResponse.StatusCode);
        Assert.NotNull(recovery);
        Assert.Matches("^[0-9]{4}$", recovery.RecoveryCode!);

        using var changeResponse = await _client.PostAsJsonAsync("/api/auth/change-password", new
        {
            email,
            recoveryCode = recovery.RecoveryCode,
            newPassword = "NuevaClave2026!"
        });
        using var reuseResponse = await _client.PostAsJsonAsync("/api/auth/change-password", new
        {
            email,
            recoveryCode = recovery.RecoveryCode,
            newPassword = "OtraClave2026!"
        });
        using var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "NuevaClave2026!"
        });

        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task FiveInvalidPasswordsLockTheAccount()
    {
        var email = $"bloqueo-{Guid.NewGuid():N}@example.local";
        await RegisterAndApproveAsync(email, "Correcta2026!");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var invalidResponse = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "Incorrecta2026!"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        }

        using var lockedResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Correcta2026!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, lockedResponse.StatusCode);
    }

    [Theory]
    [InlineData("admin@ebr.local", "ADMINISTRADOR")]
    [InlineData("empresa@ebr.local", "ADMINISTRADOR_EMPRESA")]
    [InlineData("delegado@ebr.local", "USUARIO_DELEGADO")]
    [InlineData("coordinador@ebr.local", "COORDINADOR")]
    [InlineData("tecnico@ebr.local", "TECNICO_EVALUADOR")]
    public async Task DevelopmentAccountsExposeExactlyTheirAssignedRole(string email, string expectedRole)
    {
        var session = await LoginAsync(email);

        Assert.Equal(expectedRole, session.Role);
    }

    private async Task<LoginResponse> LoginAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "EbrLocal2026!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private async Task RegisterAndApproveAsync(string email, string password)
    {
        using var registrationResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Usuario Aislado",
            documentNumber = Guid.NewGuid().ToString("N"),
            phoneNumber = "8095550101",
            email,
            password,
            requestedRole = "USUARIO_DELEGADO"
        });
        registrationResponse.EnsureSuccessStatusCode();

        var administrator = await LoginAsync("admin@ebr.local");
        using var pendingRequest = new HttpRequestMessage(HttpMethod.Get, "/api/users/pending");
        pendingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administrator.AccessToken);
        using var pendingResponse = await _client.SendAsync(pendingRequest);
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<PendingUserResponse>>();
        var user = Assert.Single(pending!, item => item.Email == email);

        using var approvalRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{user.Id}/approve");
        approvalRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", administrator.AccessToken);
        using var approvalResponse = await _client.SendAsync(approvalRequest);
        approvalResponse.EnsureSuccessStatusCode();
    }

    private sealed record LoginResponse(string AccessToken, string RefreshToken, string Role);

    private sealed record PendingUserResponse(Guid Id, string Email);

    private sealed record CurrentUserResponse(
        Guid Id,
        string Email,
        string FullName,
        string Role,
        IReadOnlyList<Guid> AuthorizedCompanyIds);

    private sealed record RecoveryResponse(string? RecoveryCode);
}
