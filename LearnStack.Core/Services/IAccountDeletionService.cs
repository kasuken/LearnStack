namespace LearnStack.Services;

public interface IAccountDeletionService
{
    /// <summary>
    /// Permanently deletes the given user's account along with every dependent
    /// row that would otherwise conflict with the database's foreign key
    /// constraints (idea/resource links, shared collections, friendships and
    /// invitations), all inside a single transaction.
    /// </summary>
    /// <param name="userId">The id of the user to delete.</param>
    /// <returns>true if the user was found and deleted; false if no such user exists.</returns>
    Task<bool> DeleteAccountAsync(string userId);
}
