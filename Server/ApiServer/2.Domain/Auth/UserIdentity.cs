using ApiServer.Domain.Users;

namespace ApiServer.Domain.Auth;

public sealed class UserIdentity
{
    public long Id { get; private set; }

    public string Uid { get; private set; } = "";
    public IdentityProvider Provider { get; private set; }
    public string ProviderSubject { get; private set; } = ""; // google sub / guest device key 등

    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation
    public User? User { get; private set; }

    private UserIdentity() { } // EF

    public UserIdentity(string uid, IdentityProvider provider, string providerSubject, DateTimeOffset now)
    {
        Uid = uid;
        Provider = provider;
        ProviderSubject = providerSubject;
        CreatedAt = now;
    }
}
