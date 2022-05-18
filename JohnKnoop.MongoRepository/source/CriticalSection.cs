using System;
using System.Threading;

namespace JohnKnoop.MongoRepository
{
    internal class CriticalSection
	{
		internal interface ICriticalSectionLock : IDisposable
		{
			bool IsLocked { get; }
		}

		private readonly SemaphoreSlim toLock = new SemaphoreSlim(1, 1);

		internal ICriticalSectionLock Lock(TimeSpan timeout) =>
			toLock.Wait(timeout) ?
				new LockReleaser() { toRelease = toLock } :
				throw new TimeoutException();

		internal ICriticalSectionLock TryLock() =>
			toLock.Wait(0) ?
				new LockReleaser() { toRelease = toLock } as ICriticalSectionLock :
				new DummyLockReleaser();

		internal ICriticalSectionLock TryLock(TimeSpan timeout) =>
			toLock.Wait(timeout) ?
					new LockReleaser() { toRelease = toLock } as ICriticalSectionLock :
					new DummyLockReleaser();

		internal struct LockReleaser : ICriticalSectionLock
		{
			internal SemaphoreSlim toRelease;
			public bool IsLocked => true;
			public void Dispose() => toRelease.Release();
		}

		internal struct DummyLockReleaser : ICriticalSectionLock
		{
			public bool IsLocked => false;
			public void Dispose() { }
		}
	}
}