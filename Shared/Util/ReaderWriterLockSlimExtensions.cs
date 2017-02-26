using System;
using System.Threading;

namespace Shared.Util
{
    public static class ReaderWriterLockSlimExtensions
    {
        private enum LockTypes
        {
            Read,
            Write,
            UpgradeableRead
        }

        private sealed class LockToken : IDisposable
        {
            private ReaderWriterLockSlim _sync;
            private readonly LockTypes _lockTypes;
            public LockToken(ReaderWriterLockSlim sync, LockTypes lockType)
            {
                _sync = sync;
                _lockTypes = lockType;
                switch (_lockTypes)
                {
                    case LockTypes.Read: _sync.EnterReadLock(); break;
                    case LockTypes.Write: _sync.EnterWriteLock(); break;
                    case LockTypes.UpgradeableRead: _sync.EnterUpgradeableReadLock(); break;
                }
            }
            public void Dispose()
            {
                if (_sync == null) return;
                switch (_lockTypes)
                {
                    case LockTypes.Read: _sync.ExitReadLock(); break;
                    case LockTypes.Write: _sync.ExitWriteLock(); break;
                    case LockTypes.UpgradeableRead: _sync.ExitUpgradeableReadLock(); break;
                }
                _sync = null;
            }
        }

        public static IDisposable Read(this ReaderWriterLockSlim obj)
        {
            return new LockToken(obj, LockTypes.Read);
        }
        public static IDisposable Write(this ReaderWriterLockSlim obj)
        {
            return new LockToken(obj, LockTypes.Write);
        }
        public static IDisposable UpgradeableRead(this ReaderWriterLockSlim obj)
        {
            return new LockToken(obj, LockTypes.UpgradeableRead);
        }
    }
}