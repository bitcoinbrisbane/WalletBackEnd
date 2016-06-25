﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class Error
    {
        public ErrorCode Code { get; set; }
        public string Message { get; set; }
    }

    public enum ErrorCode
    {
        Exception,
        ProblemInRetrivingWalletOutput,
        ProblemInRetrivingTransaction,
        NotEnoughBitcoinAvailable,
        NotEnoughAssetAvailable,
        PossibleDoubleSpend,
        AssetNotFound,
        TransactionNotSignedProperly,
        BadInputParameter,
        PersistantConcurrencyProblem,
        NoCoinsToRefund,
        NoCoinsFound,
        InvalidAddress,
        OperationNotSupported
    }
}
