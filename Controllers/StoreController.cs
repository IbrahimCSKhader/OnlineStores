using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using onlineStore.DTOs.Store;
using onlineStore.Services.Store;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace onlineStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _storeService;
        private readonly IStorefrontOriginService _storefrontOriginService;
        private readonly ILogger<StoreController> _logger;

        public StoreController(
            IStoreService storeService,
            IStorefrontOriginService storefrontOriginService,
            ILogger<StoreController> logger)
        {
            _storeService = storeService;
            _storefrontOriginService = storefrontOriginService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var stores = await _storeService.GetAllStoresAsync();
            return Ok(stores);
        }

        [HttpGet("owned")]
        [Authorize(Roles = "StoreOwner")]
        public async Task<IActionResult> GetOwned(CancellationToken cancellationToken)
        {
            var store = await _storeService.GetOwnedStoreAsync(cancellationToken);

            if (store == null)
                return NotFound(new { message = "لا يوجد متجر مرتبط بهذا الحساب" });

            return Ok(store);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            var store = await _storeService.GetStoreByIdAsync(id);

            if (store == null)
                return NotFound(new { message = "sorry the store doesnt exist" });

            return Ok(store);
        }

        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var store = await _storeService.GetStoreBySlugAsync(slug);

            if (store == null)
                return NotFound(new { message = "المتجر غير موجود" });

            return Ok(store);
        }

        [HttpGet("resolve")]
        [AllowAnonymous]
        public async Task<IActionResult> ResolveStore(
            [FromQuery] string? host = null,
            [FromQuery] string? slug = null)
        {
            var resolvedHost = string.IsNullOrWhiteSpace(host)
                ? _storefrontOriginService.GetCustomDomainHost(HttpContext)
                : host;

            if (!string.IsNullOrWhiteSpace(resolvedHost) &&
                !_storefrontOriginService.IsPlatformHost(resolvedHost))
            {
                var storeByDomain = await _storeService.GetStoreByDomainAsync(resolvedHost);

                if (storeByDomain != null)
                    return Ok(storeByDomain);
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                var storeBySlug = await _storeService.GetStoreBySlugAsync(slug);

                if (storeBySlug != null)
                    return Ok(storeBySlug);
            }

            return NotFound(new { message = "المتجر غير موجود" });
        }

        [HttpPost]
        [AllowAnonymous]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateStoreDto dto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "StoreController.Create rejected invalid model state. Keys: {ModelStateKeys}",
                    ModelState.Keys.ToArray());
                return BadRequest(ModelState);
            }

            await PopulateContactAccountsFromFormAsync(dto);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? dto.OwnerId.ToString();
            var requestContactCount = dto.ContactAccounts?.Count ?? 0;

            _logger.LogInformation(
                "StoreController.Create started. RequestedOwnerId: {RequestedOwnerId}, AuthenticatedUserId: {AuthenticatedUserId}, Slug: {Slug}, ContactAccountsCount: {ContactAccountsCount}, HasLogo: {HasLogo}, HasCoverPage: {HasCoverPage}",
                dto.OwnerId,
                userId,
                dto.Slug,
                requestContactCount,
                dto.Logo != null,
                dto.CoverPage != null);

            try
            {
                var store = await _storeService.CreateStoreAsync(dto, userId);

                _logger.LogInformation(
                    "StoreController.Create succeeded. StoreId: {StoreId}, ContactAccountsCount: {ContactAccountsCount}",
                    store.Id,
                    store.ContactAccounts.Count);

                return Ok(store);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "StoreController.Create rejected request. RequestedOwnerId: {RequestedOwnerId}, AuthenticatedUserId: {AuthenticatedUserId}, Slug: {Slug}, ContactAccountsCount: {ContactAccountsCount}",
                    dto.OwnerId,
                    userId,
                    dto.Slug,
                    requestContactCount);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "StoreController.Create failed unexpectedly. RequestedOwnerId: {RequestedOwnerId}, AuthenticatedUserId: {AuthenticatedUserId}, Slug: {Slug}, ContactAccountsCount: {ContactAccountsCount}",
                    dto.OwnerId,
                    userId,
                    dto.Slug,
                    requestContactCount);
                throw;
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStoreDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var store = await _storeService.UpdateStoreAsync(id, dto);

                if (store == null)
                    return NotFound(new { message = "المتجر غير موجود" });

                return Ok(store);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,StoreOwner")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _storeService.DeleteStoreAsync(id);

                if (!result)
                    return NotFound(new { message = "the store does not exist" });

                return Ok(new { message = "soft delete done" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        [HttpPost("{id}/visit")]
        [AllowAnonymous]
        public async Task<IActionResult> IncrementVisit(Guid id)
        {
            var visitCount = await _storeService.IncrementStoreVisitAsync(id);

            if (visitCount == null)
                return NotFound(new { message = "store does not exist" });

            return Ok(new
            {
                message = "store visit was added successfully",
                storeId = id,
                visitCount = visitCount.Value
            });
        }

        [HttpGet("{id}/visit-count")]
        [Authorize(Roles = "StoreOwner,SuperAdmin")]
        public async Task<IActionResult> GetVisitCount(Guid id)
        {
            try
            {
                var visitCount = await _storeService.GetStoreVisitCountAsync(id);

                if (visitCount == null)
                    return NotFound(new { message = "store does not exist" });

                return Ok(new
                {
                    storeId = id,
                    visitCount = visitCount.Value
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        private async Task PopulateContactAccountsFromFormAsync(CreateStoreDto dto)
        {
            if (!Request.HasFormContentType)
                return;

            if (dto.ContactAccounts != null && dto.ContactAccounts.Count > 0)
            {
                _logger.LogInformation(
                    "StoreController.Create received contact accounts from default form binding. Count: {Count}",
                    dto.ContactAccounts.Count);
                return;
            }

            var form = await Request.ReadFormAsync();
            var parsedAccounts = TryParseContactAccountsFromJsonField(form)
                ?? TryParseIndexedContactAccounts(form);

            if (parsedAccounts == null || parsedAccounts.Count == 0)
            {
                var relevantKeys = form.Keys
                    .Where(key => key.Contains("contact", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                _logger.LogWarning(
                    "StoreController.Create did not bind any contact accounts from form data. RelevantFormKeys: {RelevantFormKeys}",
                    relevantKeys);
                return;
            }

            dto.ContactAccounts = parsedAccounts;

            _logger.LogInformation(
                "StoreController.Create restored contact accounts from raw form data. Count: {Count}, Accounts: {Accounts}",
                parsedAccounts.Count,
                string.Join(
                    " | ",
                    parsedAccounts.Select((account, index) =>
                        $"#{index}:Platform={account.Platform},Username={account.Username},SortOrder={account.SortOrder},Label={account.Label ?? "<null>"}")));
        }

        private static List<StoreContactAccountInputDto>? TryParseContactAccountsFromJsonField(IFormCollection form)
        {
            var candidateKeys = new[]
            {
                "ContactAccounts",
                "contactAccounts"
            };

            foreach (var key in candidateKeys)
            {
                if (!form.TryGetValue(key, out var values))
                    continue;

                var rawValue = values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(rawValue))
                    continue;

                var trimmedValue = rawValue.Trim();
                if (!trimmedValue.StartsWith("["))
                    continue;

                try
                {
                    var parsedList = JsonSerializer.Deserialize<List<StoreContactAccountInputDto>>(
                        trimmedValue,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (parsedList != null && parsedList.Count > 0)
                        return parsedList;
                }
                catch (JsonException)
                {
                    // Ignore and fall back to indexed form parsing.
                }
            }

            return null;
        }

        private static List<StoreContactAccountInputDto>? TryParseIndexedContactAccounts(IFormCollection form)
        {
            var accountsByIndex = new Dictionary<int, StoreContactAccountInputDto>();
            var patterns = new[]
            {
                new Regex(@"^(?:ContactAccounts|contactAccounts)\[(\d+)\]\.(Platform|Username|Label|SortOrder)$", RegexOptions.IgnoreCase),
                new Regex(@"^(?:ContactAccounts|contactAccounts)\[(\d+)\]\[(Platform|Username|Label|SortOrder)\]$", RegexOptions.IgnoreCase)
            };

            foreach (var key in form.Keys)
            {
                Match? match = null;

                foreach (var pattern in patterns)
                {
                    match = pattern.Match(key);
                    if (match.Success)
                        break;
                }

                if (match == null || !match.Success)
                    continue;

                var index = int.Parse(match.Groups[1].Value);
                var propertyName = match.Groups[2].Value;
                var value = form[key].FirstOrDefault();

                if (!accountsByIndex.TryGetValue(index, out var account))
                {
                    account = new StoreContactAccountInputDto();
                    accountsByIndex[index] = account;
                }

                switch (propertyName.ToLowerInvariant())
                {
                    case "platform":
                        account.Platform = value ?? string.Empty;
                        break;
                    case "username":
                        account.Username = value ?? string.Empty;
                        break;
                    case "label":
                        account.Label = value;
                        break;
                    case "sortorder":
                        if (int.TryParse(value, out var sortOrder))
                            account.SortOrder = sortOrder;
                        break;
                }
            }

            if (accountsByIndex.Count == 0)
                return null;

            return accountsByIndex
                .OrderBy(x => x.Key)
                .Select(x => x.Value)
                .ToList();
        }
    }
}
