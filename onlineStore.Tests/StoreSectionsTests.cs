using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using onlineStore.Controllers;
using onlineStore.Data;
using onlineStore.DTOs.Store;
using onlineStore.Models;
using onlineStore.Models.Identity;
using onlineStore.Models.Offers;
using onlineStore.Security;
using onlineStore.Utilities;
namespace onlineStore.Tests;
public class StoreSectionsTests
{
 [Fact]
 public async Task SettingsAreScopedValidatedAndPreservedWhenAppearanceChanges()
 {
  await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
  await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
  await db.Database.EnsureCreatedAsync();
  var owner = new AppUser { FirstName="Test", LastName="Owner" };
  var store = new Store { Name="A", Slug="a", Owner=owner };
  var other = new Store { Name="B", Slug="b", Owner=owner };
  db.AddRange(store,other); await db.SaveChangesAsync();
  var actor = new Actor(); var ownership = new Ownership(store.Id);
  var controller = new StoreSectionsController(db,actor,ownership,null!);
  Assert.IsType<ForbidResult>(await controller.Get(other.Id,default));
  Assert.IsType<ForbidResult>(await controller.Put(other.Id,new(),default));
  Assert.IsType<ForbidResult>(await controller.Upload(other.Id,null!,default));
  var offer = new Offer {StoreId=store.Id,Name="Offer"}; db.Add(offer); await db.SaveChangesAsync();
  Assert.False(new StoreSectionsDto().ShowLocations);
  var dto = new StoreSectionsDto {ShowLocations=true, Locations=new(){new(){Name="Branch", Address="Ramallah", Phone="0568067006"}}, ShowCover=false, ShowFeaturedProducts=false, PremiumOfferId=offer.Id,
   Offers=new(){[offer.Id]=new(){Title=new(){["E"]="Office"},Images=new(){"https://example.com/photo.png"}}}};
  Assert.IsType<OkObjectResult>(await controller.Put(store.Id,dto,default));
  var appearance = new StoreAppearanceController(db);
  Assert.IsType<OkObjectResult>(await appearance.Put(store.Id,new(){Light=new(){["primary"]="#112233"}},default));
  db.ChangeTracker.Clear();
  var saved = StoreAppearancePolicy.Read((await db.Set<StoreAppearance>().SingleAsync()).OverridesJson);
  Assert.True(saved.Sections!.ShowLocations); Assert.Single(saved.Sections.Locations);
  Assert.False(saved.Sections!.ShowFeaturedProducts); Assert.False(saved.Sections.ShowCover);
  Assert.Equal("Office",saved.Sections.Offers[offer.Id].Title["E"]);
  Assert.Equal("#112233",saved.Light["primary"]);
  dto.Offers[offer.Id].Images = new(){"javascript:alert(1)"};
  Assert.IsType<BadRequestObjectResult>(await controller.Put(store.Id,dto,default));
  dto.Offers[offer.Id].Images.Clear(); dto.PremiumOfferId=Guid.NewGuid();
  Assert.IsType<BadRequestObjectResult>(await controller.Put(store.Id,dto,default));
 }
 private class Actor : ICurrentUserService { public Guid? UserId=>Guid.Empty; public bool IsAuthenticated=>true; public bool IsSuperAdmin=>false; public bool IsStoreOwner=>true; }
 private class Ownership(Guid allowed) : IStoreOwnershipService { public Task<bool> UserOwnsStoreAsync(Guid storeId,Guid userId,CancellationToken ct=default)=>Task.FromResult(storeId==allowed); }
}
