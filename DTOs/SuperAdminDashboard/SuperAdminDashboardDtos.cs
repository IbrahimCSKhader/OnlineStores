using System;
using System.Collections.Generic;
using onlineStore.Models;

namespace onlineStore.DTOs.SuperAdminDashboard
{
    public class SuperAdminDashboardSummaryDto
    {
        public int TotalStores { get; set; }
        public int ActiveStores { get; set; }
        public int InactiveStores { get; set; }
        public int TotalStoreOwners { get; set; }
        public int ActiveStoreOwners { get; set; }
        public int InactiveStoreOwners { get; set; }
        public int TotalStoreCustomers { get; set; }
        public int ActiveStoreCustomers { get; set; }
        public int InactiveStoreCustomers { get; set; }
        public int TotalContactAccounts { get; set; }
    }

    public class SuperAdminSetActivationStatusDto
    {
        public bool IsActive { get; set; }
    }

    public class SuperAdminStoreContactAccountDto
    {
        public Guid Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string Url { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class SuperAdminOwnerManagedStoreDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ThemeTemplate { get; set; } = StoreThemeTemplates.Default;
        public bool IsActive { get; set; }
        public string? StoreStory { get; set; }
        public string? WhatsAppNumber { get; set; }
        public int CustomerCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<SuperAdminStoreContactAccountDto> ContactAccounts { get; set; } = new();
    }

    public class SuperAdminOwnerListItemDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int StoreCount { get; set; }
        public int ActiveStoreCount { get; set; }
        public int InactiveStoreCount { get; set; }
        public List<SuperAdminOwnerManagedStoreDto> Stores { get; set; } = new();
    }

    public class SuperAdminOwnerDetailsDto : SuperAdminOwnerListItemDto
    {
        public DateTime? UpdatedAt { get; set; }
    }

    public class SuperAdminStoreOwnerInfoDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SuperAdminStoreListItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ThemeTemplate { get; set; } = StoreThemeTemplates.Default;
        public string? Description { get; set; }
        public string? BusinessType { get; set; }
        public bool IsActive { get; set; }
        public string? StoreStory { get; set; }
        public string? WhatsAppNumber { get; set; }
        public int CustomerCount { get; set; }
        public int VisitCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public SuperAdminStoreOwnerInfoDto? Owner { get; set; }
        public List<SuperAdminStoreContactAccountDto> ContactAccounts { get; set; } = new();
    }

    public class SuperAdminStoreDetailsDto : SuperAdminStoreListItemDto
    {
    }

    public class SuperAdminStoreCustomerDto
    {
        public Guid Id { get; set; }
        public Guid StoreId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
