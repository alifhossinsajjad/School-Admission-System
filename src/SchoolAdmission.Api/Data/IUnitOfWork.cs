namespace SchoolAdmission.Api.Data;

public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

