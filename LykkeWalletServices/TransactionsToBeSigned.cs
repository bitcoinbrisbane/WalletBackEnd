//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LykkeWalletServices
{
    using System;
    using System.Collections.Generic;
    
    public partial class TransactionsToBeSigned
    {
        public string ExchangeId { get; set; }
        public string WalletAddress { get; set; }
        public string UnsignedTransaction { get; set; }
        public string SignedTransaction { get; set; }
    
        public virtual ExchangeRequest ExchangeRequest { get; set; }
    }
}