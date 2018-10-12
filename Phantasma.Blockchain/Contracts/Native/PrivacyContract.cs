﻿using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class PrivacyContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Privacy;

        public PrivacyContract() : base()
        {
        }
    }
}