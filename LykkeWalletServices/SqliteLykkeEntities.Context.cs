﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class SqliteLykkeServicesEntities : DbContext
    {
        public SqliteLykkeServicesEntities()
            : base("name=SqliteLykkeServicesEntities")
        {
        }
    
    	// Added by developper
    	public SqliteLykkeServicesEntities(string connectionString)
            : base(connectionString)
        {
        }
    	// End of added by developper
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<ExchangeRequest> ExchangeRequests { get; set; }
        public virtual DbSet<TransactionsToBeSigned> TransactionsToBeSigneds { get; set; }
        public virtual DbSet<KeyStorage> KeyStorages { get; set; }
        public virtual DbSet<SentTransaction> SentTransactions { get; set; }
        public virtual DbSet<SpentOutput> SpentOutputs { get; set; }
    }
}
