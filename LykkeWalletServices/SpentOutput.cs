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
    
    public partial class SpentOutput
    {
        public string PrevHash { get; set; }
        public int OutputNumber { get; set; }
        public Nullable<int> SentTransactionId { get; set; }
        public byte[] Version { get; set; }
    
        public virtual SentTransaction SentTransaction { get; set; }
    }
}
