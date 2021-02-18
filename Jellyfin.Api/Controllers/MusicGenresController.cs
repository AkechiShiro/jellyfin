using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Helpers;
using Jellyfin.Api.ModelBinders;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers
{
    /// <summary>
    /// The music genres controller.
    /// </summary>
    [Authorize(Policy = Policies.DefaultAuthorization)]
    public class MusicGenresController : BaseJellyfinApiController
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IDtoService _dtoService;
        private readonly IUserManager _userManager;
        private readonly IAuthorizationContext _authContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="MusicGenresController"/> class.
        /// </summary>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/> interface.</param>
        /// <param name="userManager">Instance of <see cref="IUserManager"/> interface.</param>
        /// <param name="dtoService">Instance of <see cref="IDtoService"/> interface.</param>
        /// <param name="authContext">Instance of the <see cref="IAuthorizationContext"/> interface.</param>
        public MusicGenresController(
            ILibraryManager libraryManager,
            IUserManager userManager,
            IDtoService dtoService,
            IAuthorizationContext authContext)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
            _dtoService = dtoService;
            _authContext = authContext;
        }

        /// <summary>
        /// Gets all music genres from a given item, folder, or the entire library.
        /// </summary>
        /// <param name="startIndex">Optional. The record index to start at. All items with a lower index will be dropped from the results.</param>
        /// <param name="limit">Optional. The maximum number of records to return.</param>
        /// <param name="searchTerm">The search term.</param>
        /// <param name="parentId">Specify this to localize the search to a specific item or folder. Omit to use the root.</param>
        /// <param name="fields">Optional. Specify additional fields of information to return in the output.</param>
        /// <param name="excludeItemTypes">Optional. If specified, results will be filtered out based on item type. This allows multiple, comma delimited.</param>
        /// <param name="includeItemTypes">Optional. If specified, results will be filtered in based on item type. This allows multiple, comma delimited.</param>
        /// <param name="isFavorite">Optional filter by items that are marked as favorite, or not.</param>
        /// <param name="imageTypeLimit">Optional, the max number of images to return, per image type.</param>
        /// <param name="enableImageTypes">Optional. The image types to include in the output.</param>
        /// <param name="userId">User id.</param>
        /// <param name="nameStartsWithOrGreater">Optional filter by items whose name is sorted equally or greater than a given input string.</param>
        /// <param name="nameStartsWith">Optional filter by items whose name is sorted equally than a given input string.</param>
        /// <param name="nameLessThan">Optional filter by items whose name is equally or lesser than a given input string.</param>
        /// <param name="sortBy">Optional. Specify one or more sort orders, comma delimited.</param>
        /// <param name="sortOrder">Sort Order - Ascending,Descending.</param>
        /// <param name="enableImages">Optional, include image information in output.</param>
        /// <param name="enableTotalRecordCount">Optional. Include total record count.</param>
        /// <response code="200">Music genres returned.</response>
        /// <returns>An <see cref="OkResult"/> containing the queryresult of music genres.</returns>
        [HttpGet]
        [Obsolete("Use GetGenres instead")]
        public async Task<ActionResult<QueryResult<BaseItemDto>>> GetMusicGenresAsync(
            [FromQuery] int? startIndex,
            [FromQuery] int? limit,
            [FromQuery] string? searchTerm,
            [FromQuery] Guid? parentId,
            [FromQuery, ModelBinder(typeof(CommaDelimitedArrayModelBinder))] ItemFields[] fields,
            [FromQuery, ModelBinder(typeof(CommaDelimitedArrayModelBinder))] BaseItemKind[] excludeItemTypes,
            [FromQuery, ModelBinder(typeof(CommaDelimitedArrayModelBinder))] BaseItemKind[] includeItemTypes,
            [FromQuery] bool? isFavorite,
            [FromQuery] int? imageTypeLimit,
            [FromQuery, ModelBinder(typeof(CommaDelimitedArrayModelBinder))] ImageType[] enableImageTypes,
            [FromQuery] Guid? userId,
            [FromQuery] string? nameStartsWithOrGreater,
            [FromQuery] string? nameStartsWith,
            [FromQuery] string? nameLessThan,
            [FromQuery, ModelBinder(typeof(CommaDelimitedArrayModelBinder))] string[] sortBy,
            [FromQuery, ModelBinder(typeof(CommaDelimitedArrayModelBinder))] SortOrder[] sortOrder,
            [FromQuery] bool? enableImages = true,
            [FromQuery] bool enableTotalRecordCount = true)
        {
            if (userId.HasValue)
            {
                if (!await RequestHelpers.AssertCanUpdateUser(_authContext, HttpContext.Request, userId.Value, false).ConfigureAwait(false))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "User does not have permission for this action.");
                }
            }

            var dtoOptions = new DtoOptions { Fields = fields }
                .AddClientFields(Request)
                .AddAdditionalDtoOptions(enableImages, false, imageTypeLimit, enableImageTypes);

            User? user = userId is null || userId.Value.Equals(default)
                ? null
                : _userManager.GetUserById(userId.Value);

            var parentItem = _libraryManager.GetParentItem(parentId, userId);

            var query = new InternalItemsQuery(user)
            {
                ExcludeItemTypes = excludeItemTypes,
                IncludeItemTypes = includeItemTypes,
                StartIndex = startIndex,
                Limit = limit,
                IsFavorite = isFavorite,
                NameLessThan = nameLessThan,
                NameStartsWith = nameStartsWith,
                NameStartsWithOrGreater = nameStartsWithOrGreater,
                DtoOptions = dtoOptions,
                SearchTerm = searchTerm,
                EnableTotalRecordCount = enableTotalRecordCount,
                OrderBy = RequestHelpers.GetOrderBy(sortBy, sortOrder)
            };

            if (parentId.HasValue)
            {
                if (parentItem is Folder)
                {
                    query.AncestorIds = new[] { parentId.Value };
                }
                else
                {
                    query.ItemIds = new[] { parentId.Value };
                }
            }

            var result = _libraryManager.GetMusicGenres(query);

            var shouldIncludeItemTypes = includeItemTypes.Length != 0;
            return RequestHelpers.CreateQueryResult(result, dtoOptions, _dtoService, shouldIncludeItemTypes, user);
        }

        /// <summary>
        /// Gets a music genre, by name.
        /// </summary>
        /// <param name="genreName">The genre name.</param>
        /// <param name="userId">Optional. Filter by user id, and attach user data.</param>
        /// <returns>An <see cref="OkResult"/> containing a <see cref="BaseItemDto"/> with the music genre.</returns>
        [HttpGet("{genreName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<BaseItemDto>> GetMusicGenreAsync([FromRoute, Required] string genreName, [FromQuery] Guid? userId)
        {
            if (userId.HasValue)
            {
                if (!await RequestHelpers.AssertCanUpdateUser(_authContext, HttpContext.Request, userId.Value, false).ConfigureAwait(false))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "User does not have permission for this action.");
                }
            }

            var dtoOptions = new DtoOptions().AddClientFields(Request);

            MusicGenre? item;

            if (genreName.IndexOf(BaseItem.SlugChar, StringComparison.OrdinalIgnoreCase) != -1)
            {
                item = GetItemFromSlugName<MusicGenre>(_libraryManager, genreName, dtoOptions, BaseItemKind.MusicGenre);
            }
            else
            {
                item = _libraryManager.GetMusicGenre(genreName);
            }

            if (userId.HasValue && !userId.Value.Equals(default))
            {
                var user = _userManager.GetUserById(userId.Value);

                return _dtoService.GetBaseItemDto(item, dtoOptions, user);
            }

            return _dtoService.GetBaseItemDto(item, dtoOptions);
        }

        private T? GetItemFromSlugName<T>(ILibraryManager libraryManager, string name, DtoOptions dtoOptions, BaseItemKind baseItemKind)
            where T : BaseItem, new()
        {
            var result = libraryManager.GetItemList(new InternalItemsQuery
            {
                Name = name.Replace(BaseItem.SlugChar, '&'),
                IncludeItemTypes = new[] { baseItemKind },
                DtoOptions = dtoOptions
            }).OfType<T>().FirstOrDefault();

            result ??= libraryManager.GetItemList(new InternalItemsQuery
            {
                Name = name.Replace(BaseItem.SlugChar, '/'),
                IncludeItemTypes = new[] { baseItemKind },
                DtoOptions = dtoOptions
            }).OfType<T>().FirstOrDefault();

            result ??= libraryManager.GetItemList(new InternalItemsQuery
            {
                Name = name.Replace(BaseItem.SlugChar, '?'),
                IncludeItemTypes = new[] { baseItemKind },
                DtoOptions = dtoOptions
            }).OfType<T>().FirstOrDefault();

            return result;
        }
    }
}
