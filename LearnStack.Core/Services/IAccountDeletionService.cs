namespace LearnStack.Services;

public interface IAccountDeletionService
{
    /// <summary>
    /// Cancels the user's billing subscriptions, then permanently deletes the account along
    /// with every dependent row that would otherwise conflict with the database's foreign key
    /// constraints (idea/resource links, shared collections, friendships and invitations), all
    /// inside a single transaction.
    /// </summary>
    /// <param name="userId">The id of the user to delete.</param>
    Task<AccountDeletionResult> DeleteAccountAsync(string userId);
}

/// <summary>The outcome of <see cref="IAccountDeletionService.DeleteAccountAsync"/>.</summary>
public enum AccountDeletionResult
{
    /// <summary>The account and all its data were deleted.</summary>
    Deleted,

    /// <summary>No user with the given id exists.</summary>
    UserNotFound,

    /// <summary>
    /// The billing provider could not cancel the user's subscription, so nothing was deleted:
    /// removing the account would leave a subscription charging a user who no longer exists.
    /// </summary>
    BillingCancellationFailed,
}
