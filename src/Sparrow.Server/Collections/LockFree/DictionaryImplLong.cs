// Copyright (c) Vladimir Sadov. All rights reserved.
//
// This file is distributed under the MIT License. See LICENSE.md for details.

using System.Threading;

namespace Sparrow.Server.Collections.LockFree
{
    internal sealed class DictionaryImplLong<TValue>
                : DictionaryImpl<long, long, TValue>
    {
        internal DictionaryImplLong(int capacity, LockFreeConcurrentDictionary<long, TValue> topDict)
            : base(capacity, topDict)
        {
        }

        internal DictionaryImplLong(int capacity, DictionaryImplLong<TValue> other)
            : base(capacity, other)
        {
        }

        protected override bool TryClaimSlotForPut(ref long entryKey, long key)
        {
            return TryClaimSlot(ref entryKey, key);
        }

        protected override bool TryClaimSlotForCopy(ref long entryKey, long key)
        {
            return TryClaimSlot(ref entryKey, key);
        }

        private bool TryClaimSlot(ref long entryKey, long key)
        {
            var entryKeyValue = entryKey;
            //zero keys are claimed via hash
            if (entryKeyValue == 0 & key != 0)
            {
                entryKeyValue = Interlocked.CompareExchange(ref entryKey, key, 0);
                if (entryKeyValue == 0)
                {
                    // claimed a new slot
                    this.allocatedSlotCount.Increment();
                    return true;
                }
            }

            return key == entryKeyValue || _keyComparer.Equals(key, entryKey);
        }

        protected override int hash(long key)
        {
            if (key == 0)
            {
                return ZEROHASH;
            }

            return base.hash(key);
        }

        protected override bool keyEqual(long key, long entryKey)
        {
            return key == entryKey || _keyComparer.Equals(key, entryKey);
        }

        protected override DictionaryImpl<long, long, TValue> CreateNew(int capacity)
        {
            return new DictionaryImplLong<TValue>(capacity, this);
        }

        protected override long keyFromEntry(long entryKey)
        {
            return entryKey;
        }
    }

    internal sealed class DictionaryImplLongNoComparer<TValue>
            : DictionaryImpl<long, long, TValue>
    {
        internal DictionaryImplLongNoComparer(int capacity, LockFreeConcurrentDictionary<long, TValue> topDict)
            : base(capacity, topDict)
        {
        }

        internal DictionaryImplLongNoComparer(int capacity, DictionaryImplLongNoComparer<TValue> other)
            : base(capacity, other)
        {
        }

        protected override bool TryClaimSlotForPut(ref long entryKey, long key)
        {
            return TryClaimSlot(ref entryKey, key);
        }

        protected override bool TryClaimSlotForCopy(ref long entryKey, long key)
        {
            return TryClaimSlot(ref entryKey, key);
        }

        private bool TryClaimSlot(ref long entryKey, long key)
        {
            var entryKeyValue = entryKey;
            //zero keys are claimed via hash
            if (entryKeyValue == 0 & key != 0)
            {
                entryKeyValue = Interlocked.CompareExchange(ref entryKey, key, 0);
                if (entryKeyValue == 0)
                {
                    // claimed a new slot
                    this.allocatedSlotCount.Increment();
                    return true;
                }
            }

            return key == entryKeyValue;
        }

        protected override int hash(long key)
        {
            return (key == 0) ?
                ZEROHASH :
                key.GetHashCode() | REGULAR_HASH_BITS;
        }

        protected override bool keyEqual(long key, long entryKey)
        {
            return key == entryKey;
        }

        protected override DictionaryImpl<long, long, TValue> CreateNew(int capacity)
        {
            return new DictionaryImplLongNoComparer<TValue>(capacity, this);
        }

        protected override long keyFromEntry(long entryKey)
        {
            return entryKey;
        }
    }
}
