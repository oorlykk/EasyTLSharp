using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;
using TLSharp.Core.Exceptions;
using TLSharp.Core.Network;
using TLSharp.Core.Network.Exceptions;
using TLSharp.Core.Utils;
using Starksoft.Net.Proxy;

namespace EasyTLSharp
{
    public class EasyTLSharpClient : EasyTLSharpBase
    {
        public EasyTLSharpClient(int api_id, string api_hash, string phone_number) : 
            base(api_id, api_hash, phone_number) { }
            
        public List<TLUser> ContactList;

        public async Task UpdateContactList() 
        {
            ContactList = await GetContacts();
        }

        public new async Task<bool> Connect()
        {
            await base.Connect();
            var isconnected = TestClient();
            if (isconnected) 
                await UpdateContactList();
            return isconnected;
        }

        public async Task SendMsg(string username, string text)
        {
            InputUser = await GetContact(username, false);
            if (InputUser != null)
                await SendMsg(text);
        }

        public async Task<TLUser> GetContact(string username, bool useupdate = false)
        {
            if (useupdate)
                await UpdateContactList();

            return ContactList
                .OfType<TLUser>()
                .FirstOrDefault(c => c.Username == username);
        }
    }


    public class EasyTLSharpTalk : EasyTLSharpBase
    {
        
        public EasyTLSharpTalk(int api_id, string api_hash, string phone_number) : 
            base(api_id, api_hash, phone_number) { }

        public async Task<bool> Connect(string toUsername, bool useGloablScope = false, bool isNewOrReconnect = true)
        {
            if (isNewOrReconnect)
                await base.Connect();

            if (TestClient())
            {
                var user = useGloablScope ? await GetUserFromGlobal(toUsername) : 
                                            await GetUserFromContacts(toUsername);

                if (user != null)
                {
                    InputUser = user;
                    return true;
                }
            }

            return false;
        }

        public async Task<TLMessage> GetHistoryLastMessage()
        {
            var msgs = await GetHistory(1);
            return (msgs.Count > 0) ? msgs[0] : null;
        }

        public async Task<string> GetHistoryLastMessageText()
        {
            var msg = await GetHistoryLastMessage();
            return (msg != null) ? msg.Message : "";
        }

        public new async Task SendMsg(string text) => await base.SendMsg(text);
    }


    public abstract class EasyTLSharpBase
    {
        public TelegramClient client;
        public TLUser InputUser;
        protected TLAbsInputPeer m_InputPeer
        {
            get 
            {
                if (InputUser == null)
                {
                    return new TLInputPeerEmpty();
                }
                else
                {
                    return new TLInputPeerUser() 
                    { 
                        UserId = InputUser.Id, AccessHash = InputUser.AccessHash.Value
                    };
                }
            }
        }
        private int m_ApiID;
        private string m_ApiHash;
        private string m_PhoneNumber;
        private FileSessionStore m_SessionStore;
        public ProxyType ProxyType;
        public string ProxyHost;
        public int ProxyPort;

        public EasyTLSharpBase(int api_id, string api_hash, string phone_number)
        {
            m_ApiID = api_id;
            m_ApiHash = api_hash;
            m_PhoneNumber = phone_number;
        }

        public bool TestClient() => client != null && client.IsUserAuthorized() && client.IsConnected;

        public void RemoveSessionStore()
        {
            var sessionstore_filename = $"{m_PhoneNumber}.dat";
            if (File.Exists(sessionstore_filename)) 
                File.Delete(sessionstore_filename);
        }

        #region Auth

        public class AuthEventArgs: EventArgs
        {
            public string hash {get;set;}
            public string? code {get; set;}
        }
        
        public event EventHandler<AuthEventArgs> AuthCodeHandler;
        
        protected async Task Connect()
        {
            m_SessionStore = new FileSessionStore();

            TcpClientConnectionHandler handler = null;
            if (ProxyType != ProxyType.None)
                handler = ProxyHandler;
            
            client = new TelegramClient(m_ApiID, m_ApiHash, m_SessionStore, m_PhoneNumber, 
                                        handler: handler);
            await client.ConnectAsync();
            if (!TestClient())
            {
                var hash = await client.SendCodeRequestAsync(m_PhoneNumber);
                if (!String.IsNullOrEmpty(hash))
                {
                    var arg = new AuthEventArgs {hash = hash};
                    AuthCodeHandler?.Invoke(this, arg);
                    await client.MakeAuthAsync(m_PhoneNumber, hash, arg.code);
                }
            }
        }
        
        protected System.Net.Sockets.TcpClient ProxyHandler(string address, int port)
        {
            IProxyClient proxy;
            switch (ProxyType)
            {
                case ProxyType.Socks5:
                    proxy = new Socks5ProxyClient(ProxyHost, ProxyPort);
                    break;
                case ProxyType.Socks4:
                    proxy = new Socks4ProxyClient(ProxyHost, ProxyPort);
                    break;
                case ProxyType.Socks4a:
                    proxy = new Socks4aProxyClient(ProxyHost, ProxyPort);
                    break;
                default:
                    proxy = null;
                    break;
            }
            return proxy?.CreateConnection(address, port);
        }
        #endregion Auth

        #region H+C 
        private async Task<TLAbsMessages> _GetHistory(int count)
        {
            var msgs = await client.GetHistoryAsync(m_InputPeer, limit:count);
            if (msgs != null)
            {
                if (msgs is TLMessagesSlice)
                    return (TLMessagesSlice)msgs;
                else if (msgs is TLMessages)
                    return (TLMessages)msgs;
            }
            return null;
        }

        private async Task<TLAbsDialogs> _GetContacts()
        {
            var dlgs = await client.GetUserDialogsAsync();
            if (dlgs != null)
            {
                if (dlgs is TLDialogsSlice)
                    return (TLDialogsSlice)dlgs;
                else if (dlgs is TLDialogs)
                    return (TLDialogs)dlgs;
            }
            return null;
        }

        public async Task<List<TLUser>> GetContacts()
        {
            List<TLUser> result = new List<TLUser>();

            dynamic dialogs = await _GetContacts();
            if (dialogs is TLDialogsSlice)
                dialogs = (TLDialogsSlice)dialogs;
            else 
                dialogs = (TLDialogs)dialogs;
            foreach (var _user in dialogs.Users)
            {
                var user = (TLUser)_user;
                if (!string.IsNullOrWhiteSpace(user.Username) && user.Username != "null")
                    result.Add(user);
            }

            return result;
        }

        public async Task<List<TLMessage>> GetHistory(int count)
        {
            List<TLMessage> result = new List<TLMessage>();

            dynamic msgs = await _GetHistory(count);
            if (msgs != null)
            {
                if (msgs is TLMessagesSlice)
                    msgs = (TLMessagesSlice)msgs;
                else if (msgs is TLMessages)
                    msgs = (TLMessages)msgs; 
                    
                foreach (var msg in msgs.Messages)
                    result.Add((TLMessage)msg);
            }
            return result;
        }
        #endregion H+C

        protected async Task SendMsg(string text) =>
            await client.SendMessageAsync(m_InputPeer, text);
      
        public async Task SendBtnCallback(int messageId, byte[] btnData)
        {
            var req = new TLRequestGetBotCallbackAnswer()
            {
                MsgId = messageId,
                Peer = m_InputPeer,
                Data = btnData,
            };
            await client.SendRequestAsync<TLBotCallbackAnswer>(req);
        }

        public async Task<TLUser> GetUserFromContacts(string name)
        {
            foreach (var c in await GetContacts())
                if (c.Username == name)
                    return c;
            return null;
            // return contacts
            //     .OfType<TLUser>()
            //     .FirstOrDefault(c => c.Username == name);
        }

        public async Task<TLUser> GetUserFromGlobal(string username)
        {
            var found = await client.SearchUserAsync(username);
            if (found != null && found.Users != null && found.Users.Count > 0)
                return (TLUser)found.Users[0];
            return null;
        }
    }
    
    namespace Extensions
    {
        public static class EasyTLSharpMessageExt
        {
            public static string GetText(this TLMessage msg) => msg.Message;
            public static List<T> GetButtons<T>(this TLMessage msg) where T : TLAbsKeyboardButton
            {
                List<T> result = new List<T>();
                if (msg.ReplyMarkup != null && msg.ReplyMarkup is TLReplyInlineMarkup)
                {
                    var replyMarkup = (TLReplyInlineMarkup)msg.ReplyMarkup;
                    foreach (var keyRow in replyMarkup.Rows)
                        foreach (var keyBtn in keyRow.Buttons)
                            if (keyBtn is T) 
                                result.Add((T)keyBtn);
                }
                return result;
            }
        } 
    }
// EasyTLSharp
}
