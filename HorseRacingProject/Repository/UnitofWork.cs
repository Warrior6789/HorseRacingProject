using HorseRacingAPI.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HorseRacingAPI.Repository
{
    public class UnitofWork : IUnitofWork
    {
        private readonly DbContext _context;
        private IDbContextTransaction? _currentTransaction;
        private Dictionary<string, object>? _repositories;

        public UnitofWork(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IGenericRepository<T> GetRepository<T>() where T : class
        {
            if (_repositories == null)
            {
                _repositories = new Dictionary<string, object>();
            }

            var typeName = typeof(T).Name; 

            if (!_repositories.ContainsKey(typeName))
            {
                var repositoryInstance = new GenericRepository<T>(_context);
                _repositories.Add(typeName, repositoryInstance);
            }

            return (IGenericRepository<T>)_repositories[typeName];
        }

        public void Save()
        {
            _context.SaveChanges();
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void BeginTransaction()
        {
            _context.Database.BeginTransaction();
        }

        public void CommitTransaction()
        {
            _context.Database.CommitTransaction();
        }

        public void RollBack()
        {
            _context.Database.RollbackTransaction();
        }

        public async Task BeginTransactionAsync()
        {
            if (_currentTransaction != null)
                throw new InvalidOperationException("A transaction is already in progress.");
            _currentTransaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_currentTransaction == null) return;
            await _currentTransaction.CommitAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        public async Task RollbackTransactionAsync()
        {
            if (_currentTransaction == null) return;
            await _currentTransaction.RollbackAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        public void Dispose()
        {
            if (_currentTransaction != null)
            {
                try { _currentTransaction.Rollback(); } catch { }
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}