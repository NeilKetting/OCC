using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a vendor or supplier from whom OCC purchases materials and services.
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>Suppliers</c> table.
    /// <b>How:</b> Linked to <see cref="Order"/> (Purchase Orders) and <see cref="InventoryItem"/>.
    /// </remarks>
    public class Supplier : BaseEntity
    {

        /// <summary> Full company or trading name of the supplier. </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary> Street address of the supplier's headquarters or branch. </summary>
        public string Address { get; set; } = string.Empty;
        /// <summary> City where the supplier is based. </summary>
        public string City { get; set; } = string.Empty;
        /// <summary> Local zip or postal code. </summary>
        public string PostalCode { get; set; } = string.Empty;
        /// <summary> Primary business telephone number. </summary>
        public string Phone { get; set; } = string.Empty;
        /// <summary> Main contact person at the supplier company. </summary>
        public string ContactPerson { get; set; } = string.Empty;
        /// <summary> General inquiry or accounting email address. </summary>
        public string Email { get; set; } = string.Empty;
        /// <summary> South African VAT registration number. </summary>
        public string VatNumber { get; set; } = string.Empty;
        
        // Banking Details (Optional but good for POs)
        /// <summary> Supplier bank name for electronic funds transfers. </summary>
        public string BankName { get; set; } = string.Empty;
        /// <summary> Supplier bank account number. </summary>
        public string BankAccountNumber { get; set; } = string.Empty;
        /// <summary> Supplier bank branch code. </summary>
        public string BranchCode { get; set; } = string.Empty;

        // Our account number with this supplier
        /// <summary> The account number OCC holds with this specific supplier. </summary>
        public string SupplierAccountNumber { get; set; } = string.Empty;

        /// <summary> The branch this supplier belongs to. If null, available to all. </summary>
        public Branch? Branch { get; set; }
        
        public override string ToString() => Name;
    }
}

