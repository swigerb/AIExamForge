namespace AIExamForge.Domain.Repositories;

/// <summary>
/// Unit of Work pattern interface for coordinating repository transactions.
/// </summary>
public interface IUnitOfWork
{
    IOutlineRepository Outlines { get; }
    IFileUploadRepository FileUploads { get; }
    IGenerationRequestRepository GenerationRequests { get; }
    IQuestionSetRepository QuestionSets { get; }
    IExportRecordRepository ExportRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
