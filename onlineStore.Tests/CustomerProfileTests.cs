using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using onlineStore.Controllers;
using onlineStore.Data;
using onlineStore.DTOs.StoreCustomerAuth;
using onlineStore.Models;
using onlineStore.Models.Identity;
using onlineStore.Security;

namespace onlineStore.Tests;

public class CustomerProfileTests
{
    [Fact]
    public async Task CustomerCanUpdateOwnProfileOnlyInTheirStore()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var store = new Store { Name = "Profile", Slug = "profile", Owner = new AppUser { FirstName = "Owner", LastName = "Test" } };
        var customer = new StoreCustomer { Store = store, FirstName = "Old", LastName = "Name", Email = "profile@example.com", PasswordHash = "unchanged", EmailConfirmed = true };
        db.Add(customer); await db.SaveChangesAsync();
        var claims = new[] { new Claim(StoreCustomerClaimTypes.AccountType, StoreCustomerClaimTypes.StoreCustomerAccountType),
            new Claim(StoreCustomerClaimTypes.StoreId, store.Id.ToString()), new Claim(StoreCustomerClaimTypes.StoreCustomerId, customer.Id.ToString()) };
        var controller = new StoreCustomerProfileController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "Test")) } } };
        var request = new UpdateStoreCustomerProfileDto { FirstName = "New", LastName = "Customer", Phone = "0591234567", Address = "New street", City = "New city" };
        Assert.IsType<ForbidResult>(await controller.Update(Guid.NewGuid(), request, default));
        Assert.IsType<OkObjectResult>(await controller.Update(store.Id, request, default));
        db.ChangeTracker.Clear();
        var saved = await db.StoreCustomers.SingleAsync();
        Assert.Equal("New", saved.FirstName); Assert.Equal("New street", saved.Address);
        Assert.Equal("0591234567", saved.Phone); Assert.Equal("New city", saved.City);
        Assert.Equal("profile@example.com", saved.Email); Assert.Equal("unchanged", saved.PasswordHash);
        Assert.True(saved.EmailConfirmed);
        saved.IsActive = false; await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await controller.Get(store.Id, default));
    }
}
