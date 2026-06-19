using HorseRacingAPI.Repositories;

namespace HorseRacingAPI.Repository
{
    public interface IUnitofWork : IDisposable
    {
        IGenericRepository<T> GetRepository<T>() where T : class;
        void Save();
        Task SaveAsync();
        void BeginTransaction();
        void CommitTransaction();
        void RollBack();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
