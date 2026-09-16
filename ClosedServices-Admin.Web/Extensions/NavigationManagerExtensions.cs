using ClosedServices_Admin.Authentication;

namespace Microsoft.AspNetCore.Components;

internal static class NavigationManagerExtensions
{
    extension(NavigationManager navigationManager)
    {
        /// <summary>
        /// Checks if the redirect URL is local to the application.
        /// </summary>
        internal bool IsLocalPath(string redirectUrl)
        {
            var baseUri = new Uri(navigationManager.BaseUri, UriKind.Absolute);
            var relativeUri = new Uri(baseUri, redirectUrl);
            return relativeUri.AbsoluteUri.AsSpan().StartsWith(navigationManager.BaseUri.AsSpan(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Converts a redirect URL to a local application path.
        /// </summary>
        internal string LocalPathAndQuery(string redirectUrl)
        {
            var baseUri = new Uri(navigationManager.BaseUri, UriKind.Absolute);
            var relativeUri = new Uri(baseUri, redirectUrl);

            if (relativeUri.AbsoluteUri.AsSpan().StartsWith(navigationManager.BaseUri.AsSpan(), StringComparison.Ordinal))
            {
                return relativeUri.PathAndQuery;
            }

            return "/";
        }

        /// <summary>
        /// Navigate to a local application path.
        /// </summary>
        internal void NavigateToLocal(string redirectUrl, bool forceLoad = false)
        {
            var uri = navigationManager.LocalPathAndQuery(redirectUrl);
            navigationManager.NavigateTo(uri, forceLoad);
        }

        /// <summary>
        /// Determines whether the specified path represents an authentication-related flow.
        /// </summary>
        /// <remarks>The method checks for common authentication-related path prefixes, including 'signin', 'signout', 'signedout', and their 'account/' variants.</remarks>
        /// <returns>true if the path starts with a recognized authentication flow segment; otherwise, false.</returns>
        private static bool IsAuthenticationFlowPath(ReadOnlySpan<char> relativePath)
        {
            foreach (var authPath in AuthenticationFlow.AuthPaths)
            {
                if (relativePath.StartsWith(authPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Get the redirect URI for sign-in flows, ensuring it is properly escaped and does not lead to authentication-related loops.
        /// </summary>
        internal string SignInRedirectUri
        {
            get
            {
                // ToBaseRelativePath strips the scheme, host, and base path
                // e.g., https://localhost:7039/closedservices/services -> services
                var relativePath = navigationManager.ToBaseRelativePath(navigationManager.Uri);

                // Don't redirect to authentication-related paths to avoid loops
                if (string.IsNullOrWhiteSpace(relativePath) || IsAuthenticationFlowPath(relativePath))
                {
                    return string.Empty;
                }

                return Uri.EscapeDataString(relativePath);
            }
        }

    }
}
