﻿/*
 * Developer: Ramtin Jokar [ Ramtinak@live.com ] [ My Telegram Account: https://t.me/ramtinak ]
 * 
 * Github source: https://github.com/ramtinak/InstagramApiSharp
 * Nuget package: https://www.nuget.org/packages/InstagramApiSharp
 * 
 * IRANIAN DEVELOPERS
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Classes.Android.DeviceInfo;
using InstagramApiSharp.Classes.Models;
using InstagramApiSharp.Classes.ResponseWrappers;
using InstagramApiSharp.Converters;
using InstagramApiSharp.Converters.Json;
using InstagramApiSharp.Enums;
using InstagramApiSharp.Helpers;
using InstagramApiSharp.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InstagramApiSharp.API.Processors
{
    public class MessagingProcessor : IMessagingProcessor
    {
        private readonly AndroidDevice _deviceInfo;
        private readonly IHttpRequestProcessor _httpRequestProcessor;
        private readonly IInstaLogger _logger;
        private readonly UserSessionData _user;
        private readonly UserAuthValidate _userAuthValidate;
        public MessagingProcessor(AndroidDevice deviceInfo, UserSessionData user,
            IHttpRequestProcessor httpRequestProcessor,
            IInstaLogger logger, UserAuthValidate userAuthValidate)
        {
            _deviceInfo = deviceInfo;
            _user = user;
            _httpRequestProcessor = httpRequestProcessor;
            _logger = logger;
            _userAuthValidate = userAuthValidate;
        }
        /// <summary>
        ///     Get direct inbox threads for current user asynchronously
        /// </summary>
        /// <returns>
        ///     <see cref="T:InstagramApiSharp.Classes.Models.InstaDirectInboxContainer" />
        /// </returns>
        public async Task<IResult<InstaDirectInboxContainer>> GetDirectInboxAsync(string nextOrCursorId = "")
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var directInboxUri = UriCreator.GetDirectInboxUri(nextOrCursorId);
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, directInboxUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaDirectInboxContainer>(response, json);
                var inboxResponse = JsonConvert.DeserializeObject<InstaDirectInboxContainerResponse>(json);
                return Result.Success(ConvertersFabric.Instance.GetDirectInboxConverter(inboxResponse).Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaDirectInboxContainer>(exception.Message);
            }
        }
        /// <summary>
        ///     Get direct inbox thread by its id asynchronously
        /// </summary>
        /// <param name="threadId">Thread id</param>
        /// <returns>
        ///     <see cref="InstaDirectInboxThread" />
        /// </returns>
        public async Task<IResult<InstaDirectInboxThread>> GetDirectInboxThreadAsync(string threadId, string nextOrCursorId = "")
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var directInboxUri = UriCreator.GetDirectInboxThreadUri(threadId, nextOrCursorId);
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, directInboxUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();


                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaDirectInboxThread>(response, json);
                var threadResponse = JsonConvert.DeserializeObject<InstaDirectInboxThreadResponse>(json,
                    new InstaThreadDataConverter());

                //Reverse for Chat Order
                threadResponse.Items.Reverse();
                var converter = ConvertersFabric.Instance.GetDirectThreadConverter(threadResponse);


                return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaDirectInboxThread>(exception.Message);
            }
        }

        /// <summary>
        ///     Send new direct message. (use this function, if you didn't send any message to this user before)
        /// </summary>
        /// <param name="username">Username to send</param>
        /// <param name="text">Message text</param>
        /// <returns>List of threads</returns>
        public async Task<IResult<InstaDirectInboxThreadList>> SendNewDirectMessageAsync(string username, string text)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetRankRecipientsByUserUri(username);
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, instaUri, _deviceInfo);

                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaDirectInboxThreadList>(response, json);


                var responseRecipients = JsonConvert.DeserializeObject<InstaRankedRecipientsResponse>(json);
                var converter = ConvertersFabric.Instance.GetRecipientsConverter(responseRecipients);
                var recipients = converter.Convert();

                var firstRecipient = recipients.Users?.FirstOrDefault(rec => rec?.UserName.ToLower() == username.ToLower());
                if (firstRecipient == null)
                    return Result.UnExpectedResponse<InstaDirectInboxThreadList>(response, json);

                instaUri = UriCreator.GetParticipantRecipientUserUri(firstRecipient.Pk);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Get, instaUri, _deviceInfo);

                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaDirectInboxThreadList>(response, json);

                var respParticipant = JsonConvert.DeserializeObject<InstaDefault>(json);

                if (respParticipant.Status.ToLower() != "ok")
                    return Result.UnExpectedResponse<InstaDirectInboxThreadList>(response, json);



                instaUri = UriCreator.GetParticipantRecipientUserUri(firstRecipient.Pk);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Get, instaUri, _deviceInfo);

                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaDirectInboxThreadList>(response, json);


                var result = await SendDirectMessageAsync(firstRecipient.Pk.ToString(), null, text);

                return result;
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaDirectInboxThreadList>(exception);
            }
        }
        /// <summary>
        ///     Send direct message to provided users and threads
        /// </summary>
        /// <param name="recipients">Comma-separated users PK</param>
        /// <param name="threadIds">Message thread ids</param>
        /// <param name="text">Message text</param>
        /// <returns>List of threads</returns>
        public async Task<IResult<InstaDirectInboxThreadList>> SendDirectMessageAsync(string recipients, string threadIds,
            string text)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            var threads = new InstaDirectInboxThreadList();
            try
            {
                var directSendMessageUri = UriCreator.GetDirectSendMessageUri();
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, directSendMessageUri, _deviceInfo);
                var fields = new Dictionary<string, string> { { "text", text } };
                if (!string.IsNullOrEmpty(recipients))
                    fields.Add("recipient_users", "[[" + recipients + "]]");
                else
                    return Result.Fail<InstaDirectInboxThreadList>("Please provide at least one recipient.");
                if (!string.IsNullOrEmpty(threadIds))
                    fields.Add("thread_ids", "[" + threadIds + "]");

                request.Content = new FormUrlEncodedContent(fields);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaDirectInboxThreadList>(response, json);
                var result = JsonConvert.DeserializeObject<InstaSendDirectMessageResponse>(json);
                if (!result.IsOk()) return Result.Fail<InstaDirectInboxThreadList>(result.Status);
                threads.AddRange(result.Threads.Select(thread =>
                    ConvertersFabric.Instance.GetDirectThreadConverter(thread).Convert()));
                return Result.Success(threads);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaDirectInboxThreadList>(exception);
            }
        }
        /// <summary>
        ///     Get recent recipients (threads and users) asynchronously
        /// </summary>
        /// <returns>
        ///     <see cref="InstaRecipients" />
        /// </returns>
        public async Task<IResult<InstaRecipients>> GetRecentRecipientsAsync()
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var userUri = UriCreator.GetRecentRecipientsUri();
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, userUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaRecipients>(response, json);
                var responseRecipients = JsonConvert.DeserializeObject<InstaRecentRecipientsResponse>(json);
                var converter = ConvertersFabric.Instance.GetRecipientsConverter(responseRecipients);
                return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaRecipients>(exception.Message);
            }
        }
        /// <summary>
        ///     Get ranked recipients (threads and users) asynchronously
        /// </summary>
        /// <returns>
        ///     <see cref="InstaRecipients" />
        /// </returns>
        public async Task<IResult<InstaRecipients>> GetRankedRecipientsAsync()
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var userUri = UriCreator.GetRankedRecipientsUri();
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, userUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaRecipients>(response, json);
                var responseRecipients = JsonConvert.DeserializeObject<InstaRankedRecipientsResponse>(json);
                var converter = ConvertersFabric.Instance.GetRecipientsConverter(responseRecipients);
                return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaRecipients>(exception.Message);
            }
        }
        /// <summary>
        ///     Approve direct pending request
        /// </summary>
        /// <param name="threadId">Thread id</param>
        public async Task<IResult<bool>> ApproveDirectPendingRequestAsync(params string[] threadIds)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {

                var data = new Dictionary<string, string>
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()}
                };
                Uri instaUri;
                if (threadIds.Length == 1)
                    instaUri = UriCreator.GetApprovePendingDirectRequestUri(threadIds.FirstOrDefault());
                else
                {
                    instaUri = UriCreator.GetApprovePendingMultipleDirectRequestUri();
                    data.Add("thread_ids", threadIds.EncodeList(false));
                }
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefaultResponse>(json);
                if (obj.IsSucceed)
                    return Result.Success(true);
                else
                    return Result.Fail("Error: " + obj.Message, false);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception.Message);
            }
        }
        /// <summary>
        ///     Decline all direct pending requests
        /// </summary>
        public async Task<IResult<bool>> DeclineAllDirectPendingRequestsAsync()
        {
            return await DeclineDirectPendingRequests(true);
        }
        public async Task<IResult<bool>> DeclineDirectPendingRequestsAsync(params string[] threadIds)
        {
            return await DeclineDirectPendingRequests(false, threadIds);
        }
        private async Task<IResult<bool>> DeclineDirectPendingRequests(bool all, params string[] threadIds)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetDeclineAllPendingDirectRequestsUri();

                var data = new Dictionary<string, string>
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()}
                };
                if(!all)
                {
                    if(threadIds.Length == 1)
                        instaUri = UriCreator.GetDeclinePendingDirectRequestUri(threadIds.FirstOrDefault());
                    else
                    {
                        instaUri = UriCreator.GetDeclineMultplePendingDirectRequestsUri();
                        data.Add("thread_ids", threadIds.EncodeList(false));
                    }
                }
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefaultResponse>(json);
                if (obj.IsSucceed)
                    return Result.Success(true);
                else
                    return Result.Fail("Error: " + obj.Message, false);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception.Message);
            }
        }
        /// <summary>
        ///     Get direct pending inbox threads for current user asynchronously
        /// </summary>
        /// <returns>
        ///     <see cref="T:InstagramApiSharp.Classes.Models.InstaDirectInboxContainer" />
        /// </returns>
        public async Task<IResult<InstaDirectInboxContainer>> GetPendingDirectAsync(string nextOrCursorId = "")
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var directInboxUri = UriCreator.GetDirectPendingInboxUri(nextOrCursorId);
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, directInboxUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaDirectInboxContainer>(response, json);
                var inboxResponse = JsonConvert.DeserializeObject<InstaDirectInboxContainerResponse>(json);
                return Result.Success(ConvertersFabric.Instance.GetDirectInboxConverter(inboxResponse).Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaDirectInboxContainer>(exception.Message);
            }
        }
        /// <summary>
        ///     Share an user
        /// </summary>
        /// <param name="userIdToSend">User id(PK)</param>
        /// <param name="threadId">Thread id</param>
        public async Task<IResult<InstaSharing>> ShareUserAsync(string userIdToSend, string threadId)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetShareUserUri();
                var uploadId = ApiRequestMessage.GenerateUploadId();
                var requestContent = new MultipartFormDataContent(uploadId)
                {
                    {new StringContent(userIdToSend), "\"profile_user_id\""},
                    {new StringContent("1"), "\"unified_broadcast_format\""},
                    {new StringContent("send_item"), "\"action\""},
                    {new StringContent($"[{threadId}]"), "\"thread_ids\""},
                    {new StringContent(_deviceInfo.DeviceGuid.ToString()), "\"_uuid\""},
                    {new StringContent(_user.LoggedInUser.Pk.ToString()), "\"_uid\""},
                    {new StringContent(_user.CsrfToken), "\"_csrftoken\""}

                };
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo);
                request.Content = requestContent;
                request.Headers.Add("Host", "i.instagram.com");
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.Fail("Status code: " + response.StatusCode, (InstaSharing)null);
                var obj = JsonConvert.DeserializeObject<InstaSharing>(json);

                return Result.Success(obj);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<InstaSharing>(exception);
            }
        }

        /// <summary>
        ///     Send photo to direct thread (single)
        /// </summary>
        /// <param name="image">Image to upload</param>
        /// <param name="threadId">Thread id</param>
        /// <returns>Returns True is sent</returns>
        public async Task<IResult<bool>> SendDirectPhotoAsync(InstaImage image, string threadId)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            return await SendDirectPhoto(null, threadId, image);
        }
        /// <summary>
        ///     Send photo to multiple recipients (multiple user)
        /// </summary>
        /// <param name="image">Image to upload</param>
        /// <param name="recipients">Recipients (user ids/pk)</param>
        /// <returns>Returns True is sent</returns>
        public async Task<IResult<bool>> SendDirectPhotoToRecipientsAsync(InstaImage image, params string[] recipients)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            return await SendDirectPhoto(string.Join(",", recipients), null, image);
        }
        private async Task<IResult<bool>> SendDirectPhoto(string recipients, string threadId, InstaImage image)
        {
            try
            {
                Debug.WriteLine(threadId);
                Debug.WriteLine(recipients);
                var instaUri = UriCreator.GetDirectSendPhotoUri();
                var uploadId = ApiRequestMessage.GenerateRandomUploadId();
                var clientContext = Guid.NewGuid();
                var requestContent = new MultipartFormDataContent(uploadId)
                {
                    {new StringContent("send_item"), "\"action\""},
                    {new StringContent(clientContext.ToString()), "\"client_context\""},
                    {new StringContent(_user.CsrfToken), "\"_csrftoken\""},
                    {new StringContent(_deviceInfo.DeviceGuid.ToString()), "\"_uuid\""}
                };
                if (!string.IsNullOrEmpty(recipients))
                    requestContent.Add(new StringContent($"[[{recipients}]]"), "recipient_users");
                else
                    requestContent.Add(new StringContent($"[{threadId}]"), "thread_ids");
                byte[] fileBytes;
                if (image.ImageBytes == null)
                    fileBytes = File.ReadAllBytes(image.Uri);
                else
                    fileBytes = image.ImageBytes;
                var imageContent = new ByteArrayContent(fileBytes);
                imageContent.Headers.Add("Content-Transfer-Encoding", "binary");
                imageContent.Headers.Add("Content-Type", "application/octet-stream");
                requestContent.Add(imageContent, "photo",
                    $"direct_temp_photo_{ApiRequestMessage.GenerateUploadId()}.jpg");
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo);
                request.Content = requestContent;
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);

                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                if (obj.Status.ToLower() == "ok")
                    return Result.Success(true);
                else
                    return Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }

        /// <summary>
        ///     Send video to direct thread (single)
        /// </summary>
        /// <param name="video">Video to upload (no need to set thumbnail)</param>
        /// <param name="threadId">Thread id</param>
        public async Task<IResult<bool>> SendDirectVideoAsync(InstaVideoUpload video, string threadId)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            return await SendDirectVideo(null, threadId, video);
        }
        /// <summary>
        ///     Send video to multiple recipients (multiple user)
        /// </summary>
        /// <param name="video">Video to upload (no need to set thumbnail)</param>
        /// <param name="recipients">Recipients (user ids/pk)</param>
        public async Task<IResult<bool>> SendDirectVideoToRecipientsAsync(InstaVideoUpload video, params string[] recipients)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            return await SendDirectVideo(string.Join(",", recipients), null, video);
        }
        private async Task<IResult<bool>> SendDirectVideo(string recipients, string threadId, InstaVideoUpload video)
        {
            try
            {
                var uploadId = ApiRequestMessage.GenerateRandomUploadId();
                var videoHashCode = Path.GetFileName(video.Video.Uri).GetHashCode();
                var waterfallId = Guid.NewGuid().ToString();
                var clientContext = Guid.NewGuid().ToString();
                var videoEntityName = string.Format("{0}_0_{1}", uploadId, videoHashCode);
                var videoUri = UriCreator.GetStoryUploadVideoUri(uploadId, videoHashCode);

                var videoUploadParamsObj = new JObject
                {
                    {"upload_media_height", "0"},
                    {"direct_v2", "1"},
                    {"upload_media_width", "0"},
                    {"upload_media_duration_ms", "0"},
                    {"upload_id", uploadId},
                    {"retry_context", "{\"num_step_auto_retry\":0,\"num_reupload\":0,\"num_step_manual_retry\":0}"},
                    {"media_type", "2"}
                };
                var videoUploadParams = JsonConvert.SerializeObject(videoUploadParamsObj);
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, videoUri, _deviceInfo);
                request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);


                byte[] videoBytes;
                if (video.Video.VideoBytes == null)
                    videoBytes = File.ReadAllBytes(video.Video.Uri);
                else
                    videoBytes = video.Video.VideoBytes;

                var videoContent = new ByteArrayContent(videoBytes);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, videoUri, _deviceInfo);
                request.Content = videoContent;
                var vidExt = Path.GetExtension(video.Video.Uri).Replace(".", "").ToLower();
                if (vidExt == "mov")
                    request.Headers.Add("X-Entity-Type", "video/quicktime");
                else
                    request.Headers.Add("X-Entity-Type", "video/mp4");
                request.Headers.Add("Offset", "0");
                request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                request.Headers.Add("X-Entity-Name", videoEntityName);
                request.Headers.Add("X-Entity-Length", videoBytes.Length.ToString());
                request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);



                var data = new Dictionary<string, string>()
                {
                     {"action","send_item"},
                     {"client_context",clientContext.ToString()},
                     {"_csrftoken",_user.CsrfToken},
                     {"video_result",""},
                     {"_uuid",_deviceInfo.DeviceGuid.ToString()},
                     {"upload_id",uploadId}
                };
                if (!string.IsNullOrEmpty(recipients))
                    data.Add("recipient_users", $"[[{recipients}]]");
                else
                    data.Add("thread_ids", $"[{threadId}]");
                var directConfigUri = UriCreator.GetDirectConfigVideoUri();
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, directConfigUri, _deviceInfo);
                request.Content = new FormUrlEncodedContent(data);
                request.Headers.Add("retry_context", "{\"num_step_auto_retry\":0,\"num_reupload\":0,\"num_step_manual_retry\":0}");
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);

                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                if (obj.Status.ToLower() == "ok")
                    return Result.Success(true);
                else
                    return Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }
        /// <summary>
        ///     Mark direct message as seen
        /// </summary>
        /// <param name="threadId">Thread id</param>
        /// <param name="itemId">Message id (item id)</param>
        public async Task<IResult<bool>> MarkDirectThreadAsSeenAsync(string threadId, string itemId)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetDirectThreadSeenUri(threadId);

                var data = new JObject
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uid", _user.LoggedInUser.Pk.ToString()},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"item_ids", $"[{itemId}]"},
                };
                var request =
                    HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }
        /// <summary>
        ///     Update direct thread title (for groups)
        /// </summary>
        /// <param name="threadId">Thread id</param>
        /// <param name="title">New title</param>
        public async Task<IResult<bool>> UpdateDirectThreadAsync(string threadId, string title)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetDirectThreadUpdateTitleUri(threadId);

                var data = new Dictionary<string,string>
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"title", title},
                };
                var request =
                    HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }
        /// <summary>
        ///     Mute direct thread
        /// </summary>
        /// <param name="threadId">Thread id</param>
        public async Task<IResult<bool>> MuteDirectThreadAsync(string threadId)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetMuteDirectThreadUri(threadId);

                var data = new Dictionary<string, string>
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()}
                };
                var request =
                    HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }
        /// <summary>
        ///     Unmute direct thread
        /// </summary>
        /// <param name="threadId">Thread id</param>
        public async Task<IResult<bool>> UnMuteDirectThreadAsync(string threadId)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetUnMuteDirectThreadUri(threadId);

                var data = new Dictionary<string, string>
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()}
                };
                var request =
                    HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }
        /// <summary>
        ///     Send profile to direct thrad
        /// </summary>
        /// <param name="userIdToSend">User id to send</param>
        /// <param name="threadIds">Thread ids</param>
        public async Task<IResult<bool>> SendDirectProfileAsync(long userIdToSend, params string[] threadIds)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetSendDirectProfileUri();
                var clientContext = Guid.NewGuid().ToString();
                var data = new Dictionary<string, string>
                {
                    {"profile_user_id", userIdToSend.ToString()},
                    {"action", "send_item"},
                    {"thread_ids", $"[{threadIds.EncodeList(false)}]"},
                    {"client_context", clientContext},
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()}
                };
                var request =
                    HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }
        /// <summary>
        ///     Send link address to direct thread
        /// </summary>
        /// <param name="text">Text to send</param>
        /// <param name="link">Link to send</param>
        /// <param name="threadIds">Thread ids</param>
        public async Task<IResult<bool>> SendDirectLinkAsync(string text, string link, params string[] threadIds)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetSendDirectLinkUri();
                var clientContext = Guid.NewGuid().ToString();
                var data = new Dictionary<string, string>
                {
                    {"link_text", text},
                    {"link_urls", $"[{ExtensionHelper.EncodeList(new string[]{ link })}]"},
                    {"action", "send_item"},
                    {"thread_ids", $"[{threadIds.EncodeList(false)}]"},
                    {"client_context", clientContext},
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()}
                };

                var request =
                    HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }
        /// <summary>
        ///     Send location to direct thread
        /// </summary>
        /// <param name="externalId">External id (get it from <seealso cref="LocationProcessor.SearchLocationAsync"/></param>
        /// <param name="threadIds">Thread ids</param>
        public async Task<IResult<bool>> SendDirectLocationAsync(string externalId, params string[] threadIds)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {
                var instaUri = UriCreator.GetSendDirectLocationUri();
                var clientContext = Guid.NewGuid().ToString();
                var data = new Dictionary<string, string>
                {
                    {"venue_id", externalId},
                    {"action", "send_item"},
                    {"thread_ids", $"[{threadIds.EncodeList(false)}]"},
                    {"client_context", clientContext},
                    {"_csrftoken", _user.CsrfToken},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()}
                };

                var request =
                    HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);
                var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }

        public async Task<IResult<bool>> SendDirectDisappearingVideoAsync(InstaVideoUpload video,
InstaViewMode viewMode = InstaViewMode.Replayable, params string[] threadIds)
        {
            UserAuthValidator.Validate(_userAuthValidate);
            try
            {

                var uploadId = ApiRequestMessage.GenerateRandomUploadId();
                var videoHashCode = Path.GetFileName(video.Video.Uri).GetHashCode();
                var photoHashCode = Path.GetFileName(video.VideoThumbnail.Uri).GetHashCode();

                var waterfallId = Guid.NewGuid().ToString();

                var videoEntityName = string.Format("{0}_0_{1}", uploadId, videoHashCode);
                var videoUri = UriCreator.GetStoryUploadVideoUri(uploadId, videoHashCode);

                var photoEntityName = string.Format("{0}_0_{1}", uploadId, photoHashCode);
                var photoUri = UriCreator.GetStoryUploadPhotoUri(uploadId, photoHashCode);


                var videoMediaInfoData = new JObject
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uid", _user.LoggedInUser.Pk},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"media_info", new JObject
                        {
                                {"capture_mode", "normal"},
                                {"media_type", 2},
                                {"caption", ""},
                                {"mentions", new JArray()},
                                {"hashtags", new JArray()},
                                {"locations", new JArray()},
                                {"stickers", new JArray()},
                        }
                    }
                };
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, UriCreator.GetStoryMediaInfoUploadUri(), _deviceInfo, videoMediaInfoData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                var videoUploadParamsObj = new JObject
                {
                    {"upload_media_height", "0"},
                    {"upload_media_width", "0"},
                    {"upload_media_duration_ms", "0"},
                    {"upload_id", uploadId},
                    {"retry_context", "{\"num_step_auto_retry\":0,\"num_reupload\":0,\"num_step_manual_retry\":0}"},
                    {"media_type", "2"},
                    {"for_direct_story", "1"}
                };

                var videoUploadParams = JsonConvert.SerializeObject(videoUploadParamsObj);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Get, videoUri, _deviceInfo);
                request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);


                byte[] videoBytes;
                if (video.Video.VideoBytes == null)
                    videoBytes = File.ReadAllBytes(video.Video.Uri);
                else
                    videoBytes = video.Video.VideoBytes;
                var videoContent = new ByteArrayContent(videoBytes);
                videoContent.Headers.Add("Content-Transfer-Encoding", "binary");
                videoContent.Headers.Add("Content-Type", "application/octet-stream");
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, videoUri, _deviceInfo);
                request.Content = videoContent;
                var vidExt = Path.GetExtension(video.Video.Uri).Replace(".", "").ToLower();
                if (vidExt == "mov")
                    request.Headers.Add("X-Entity-Type", "video/quicktime");
                else
                    request.Headers.Add("X-Entity-Type", "video/mp4");
                request.Headers.Add("Offset", "0");
                request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                request.Headers.Add("X-Entity-Name", videoEntityName);
                request.Headers.Add("X-Entity-Length", videoBytes.Length.ToString());
                request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);


                // thumbnail upload
                var photoUploadParamsObj = new JObject
                {
                    {"retry_context", "{\"num_step_auto_retry\":0,\"num_reupload\":0,\"num_step_manual_retry\":0}"},
                    {"media_type", "2"},
                    {"upload_id", uploadId},
                    {"image_compression", "{\"lib_name\":\"moz\",\"lib_version\":\"3.1.m\",\"quality\":\"95\"}"},
                };
                var photoUploadParams = JsonConvert.SerializeObject(photoUploadParamsObj);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Get, photoUri, _deviceInfo);
                request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                request.Headers.Add("X-Instagram-Rupload-Params", photoUploadParams);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);


                byte[] imageBytes;
                if (video.VideoThumbnail.ImageBytes == null)
                    imageBytes = File.ReadAllBytes(video.VideoThumbnail.Uri);
                else
                    imageBytes = video.VideoThumbnail.ImageBytes;
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.Add("Content-Transfer-Encoding", "binary");
                imageContent.Headers.Add("Content-Type", "application/octet-stream");
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, photoUri, _deviceInfo);
                request.Content = imageContent;
                request.Headers.Add("X-Entity-Type", "image/jpeg");
                request.Headers.Add("Offset", "0");
                request.Headers.Add("X-Instagram-Rupload-Params", photoUploadParams);
                request.Headers.Add("X-Entity-Name", photoEntityName);
                request.Headers.Add("X-Entity-Length", imageBytes.Length.ToString());
                request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return await ConfigureDisappearingVideoAsync(video, uploadId, viewMode, threadIds);
                return Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception.Message);
            }
        }
        private async Task<IResult<bool>> ConfigureDisappearingVideoAsync(InstaVideoUpload video, string uploadId,
    InstaViewMode viewMode, params string[] threadIds)
        {
            try
            {
                var instaUri = UriCreator.GetVideoStoryConfigureUri(true);
                Random rnd = new Random();
                var convertedThreads = ExtensionHelper.EncodeList(threadIds);
                var data = new JObject
                {
                    {"filter_type", "0"},
                    {"timezone_offset", "16200"},
                    {"view_mode", viewMode.ToString().ToLower()},
                    {"_csrftoken", _user.CsrfToken},
                    {"client_shared_at", (long.Parse(ApiRequestMessage.GenerateUploadId())- rnd.Next(25,55)).ToString()},
                    {"story_media_creation_date", (long.Parse(ApiRequestMessage.GenerateUploadId())- rnd.Next(50,70)).ToString()},
                    {"media_folder", "Camera"},
                    {"configure_mode", "2"},
                    {"recipient_users", "[]"},
                    {"thread_ids", $"[{convertedThreads}]"},
                    {"source_type", "4"},
                    {"video_result", ""},
                    {"_uid", _user.LoggedInUser.Pk.ToString()},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"caption", ""},
                    {"date_time_original", DateTime.Now.ToString("yyyy-dd-MMTh:mm:ss-0fffZ")},
                    {"capture_type", "normal"},
                    {"mas_opt_in", "NOT_PROMPTED"},
                    {"upload_id", uploadId},
                    {"client_timestamp", ApiRequestMessage.GenerateUploadId()},
                    {
                        "device", new JObject{
                            {"manufacturer", _deviceInfo.HardwareManufacturer},
                            {"model", _deviceInfo.DeviceModelIdentifier},
                            {"android_release", "7.0"},
                            {"android_version", 24}
                        }
                    },
                    {"length", 0},
                    {
                        "extra", new JObject
                        {
                            {"source_width", 0},
                            {"source_height", 0}
                        }
                    },
                    {"audio_muted", false},
                    {"poster_frame_index", 0},
                };


                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var uploadParamsObj = new JObject
                {
                    {"num_step_auto_retry", 0},
                    {"num_reupload", 0},
                    {"num_step_manual_retry", 0}
                };
                var uploadParams = JsonConvert.SerializeObject(uploadParamsObj);
                request.Headers.Add("retry_context", uploadParams);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(json);
                if (response.IsSuccessStatusCode)
                {
                    var mediaResponse = JsonConvert.DeserializeObject<InstaDefault>(json);

                    return mediaResponse.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
                }
                return Result.UnExpectedResponse<bool>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception.Message);
            }
        }
    }
}