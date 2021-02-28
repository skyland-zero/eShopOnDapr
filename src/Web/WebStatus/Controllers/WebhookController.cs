using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebStatus.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : Controller
    {

        private readonly IConfiguration _configuration;
        private readonly List<WorkWeChatOptions> _workWeChatOptions;
        private Dictionary<string, WorkWeChatToken> _workWeChatTokens = new Dictionary<string, WorkWeChatToken>();

        public WebhookController(IConfiguration configuration)
        {
            _configuration = configuration;
            _workWeChatOptions = _configuration.GetSection("WorkWeChatOptions").Get<List<WorkWeChatOptions>>();
        }

        [HttpPost]
        [Route("WorkWeChat")]
        public async Task<dynamic> WorkWeChat([FromQuery]string key)
        {
             if (!_workWeChatOptions.Any(x => x.key == key)) return new { code = -1, msg = "key错误，请检查配置" };
            var option = _workWeChatOptions.Where(x => x.key == key).FirstOrDefault();
            if (string.IsNullOrEmpty(option.corpid)) return new { code = -1, msg = "企业ID不能为空，请检查配置" };
            if (string.IsNullOrEmpty(option.corpsecret)) return new { code = -1, msg = "应用的凭证密钥不能为空，请检查配置" };
           
            string token = null;
            if (_workWeChatTokens.TryGetValue(key, out var outToken))
            {
                if (outToken.ExpiresTime.AddSeconds(-20) >= DateTime.Now) token = outToken.AccessToken;
                else _workWeChatTokens.Remove(key);
            }

            if (token == null)
            {
                var url = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={option.corpid}&corpsecret={option.corpsecret}";
                var res = await url.GetAsync().ReceiveJson<dynamic>();
                if(res.errcode != 0) return new { code = -1, msg = $"请求企业微信AccessToken失败：{res.errmsg}" };
                token = res.access_token;
                _workWeChatTokens.TryAdd(key, new WorkWeChatToken { ExpiresTime = DateTime.Now.AddSeconds((double)res.expires_in), AccessToken = token });
            }

            using StreamReader reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8);
            var msgUrl = $"https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token={token}";
            var body = await reader.ReadToEndAsync();
            var data = await msgUrl.PostStringAsync(body).ReceiveString();
            return new { code = 0, msg = $"已调用企业微信信息发送接口", data};
        }





    }

    public class WorkWeChatOptions
    {
        /// <summary>
        /// 消息发送key，用于验证webhook请求和发送到对应的agentid
        /// </summary>
        public string key { get; set; }

        /// <summary>
        /// 企业ID(https://open.work.weixin.qq.com/api/doc/90000/90135/91039#14953/corpid)
        /// </summary>
        public string corpid { get; set; }

        /// <summary>
        /// 应用的凭证密钥(https://open.work.weixin.qq.com/api/doc/90000/90135/91039#14953/secret)
        /// </summary>
        public string corpsecret { get; set; }
    }

    public class WorkWeChatToken
    {
        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime ExpiresTime { get; set; }

        /// <summary>
        /// AccessToken
        /// </summary>
        public string AccessToken { get; set; }
    }

}
