namespace Orbis.Domain.Identity;

public sealed class UserAccount
{
    public Guid Id { get; private set; }
    public bool IsActive { get; private set; }

    private UserAccount() { }

    public UserAccount(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("User identity is required.", nameof(id));
        Id = id;
        IsActive = true;
    }

    public void Suspend() => IsActive = false;
}
