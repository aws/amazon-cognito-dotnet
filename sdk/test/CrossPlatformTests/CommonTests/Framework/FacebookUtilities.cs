using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ThirdParty.Json.LitJson;

namespace CommonTests.IntegrationTests.Utils
{
    public static class FacebookUtilities
    {
        public static async Task DeleteFacebookUserAsync(FacebookCreateUserResponse user)
        {
            var facebookDeleteUserUrl = string.Format("https://graph.facebook.com/{0}?method=delete&access_token={1}",
            user.ID, user.AccessToken);
            var deleteUserRequest = new HttpRequestMessage();
            deleteUserRequest.RequestUri = new Uri(facebookDeleteUserUrl);
            var deleteUserHttpClient = new HttpClient();
            var deleteUserResponse =await deleteUserHttpClient.SendAsync(deleteUserRequest).ConfigureAwait(false);

            var response = await deleteUserResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public static async Task<FacebookCreateUserResponse> CreateFacebookUser(string facebookAppId, string facebookAppSecret)
        {
            var accessToken = await GetFacebookAccessToken(facebookAppId, facebookAppSecret).ConfigureAwait(false) ;

            FacebookCreateUserResponse responseObject;
            // Create an user for the app. This returns the OAuth token.
            // https://developers.facebook.com/docs/test_users/
            var facebookCreateNewUserUrl = string.Format(
                @"https://graph.facebook.com/{0}/accounts/test-users?
                installed=true&name=Foo%20Bar&locale=en_US&permissions=read_stream&method=post&{1}",
                facebookAppId,
                accessToken);

            var createUserRequest = new HttpRequestMessage();
            createUserRequest.RequestUri = new Uri(facebookCreateNewUserUrl);
            var createUserHttpClient = new HttpClient();
            var createUserResponse =await createUserHttpClient.SendAsync(createUserRequest).ConfigureAwait(false);

            var response = await createUserResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            var json = ThirdParty.Json.LitJson.JsonMapper.ToObject(response);
            responseObject = new FacebookCreateUserResponse(json);

            return responseObject;
        }

        private static async Task<string> GetFacebookAccessToken(string facebookAppId, string facebookAppSecret)
        {
            // https://developers.facebook.com/docs/opengraph/howtos/publishing-with-app-token/
            var facebookAppAccessTokenUri = string.Format(
                "https://graph.facebook.com/oauth/access_token?client_id={0}&client_secret={1}&grant_type=client_credentials",
                facebookAppId,
                facebookAppSecret);

            // Get App Access Token
            string accessToken;


            var accessTokenRequest = new HttpRequestMessage();
            accessTokenRequest.RequestUri = new Uri(facebookAppAccessTokenUri);

            var requestClient = new HttpClient();
            var accessTokenResponse = await requestClient.SendAsync(accessTokenRequest).ConfigureAwait(false);

            accessToken = await accessTokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false);


            return accessToken;
        }

        public class FacebookCreateUserResponse
        {
            public string ID { get; private set; }

            public string Email { get; private set; }

            public string AccessToken { get; private set; }

            public string LoginUrl { get; private set; }

            public string Password { get; private set; }

            public FacebookCreateUserResponse(JsonData json)
            {
                ID = GetValue(json, "id");
                Email = GetValue(json, "email");
                AccessToken = GetValue(json, "access_token");
                LoginUrl = GetValue(json, "login_url");
                Password = GetValue(json, "password");
            }
            private static string GetValue(JsonData json, string name)
            {
                var value = json[name];
                return (value == null) ? null : value.ToString();
            }
        }
    }
}
