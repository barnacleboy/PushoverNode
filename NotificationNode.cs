using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Security;
using LogicModule.Nodes.Helpers;
using LogicModule.ObjectModel;
using LogicModule.ObjectModel.TypeSystem;

namespace christian_schwarz_gmx_de.Logic.PushoverNode
{
    public class NotificationNode : LogicNodeBase
    {
        public NotificationNode(INodeContext context)
            : base(context)
        {
            context.ThrowIfNull("context");

            this.SetupIgnoreSSLTrust();

            var typeService = context.GetService<ITypeService>();

            this.Trigger = typeService.CreateBool(PortTypes.Binary, "Trigger");
            this.Token = typeService.CreateString(PortTypes.String, "Token");
            this.UserKey = typeService.CreateString(PortTypes.String, "UserKey");
            this.Message = typeService.CreateString(PortTypes.String, "Message");
            this.Attachment = typeService.CreateString(PortTypes.String, "Attachment");

            this.Variables = new List<StringValueObject>();

            this.VariableCount = typeService.CreateInt(PortTypes.Integer, "VariableCount", 0);
            this.VariableCount.MinValue = 0;
            this.VariableCount.MaxValue = 99;

            ListHelpers.ConnectListToCounter(this.Variables, this.VariableCount, typeService.GetValueObjectCreator(PortTypes.String, "Variable"), null, null);
        }

        [Input(DisplayOrder = 1)]
        public BoolValueObject Trigger { get; private set; }

        [Parameter(DisplayOrder = 2, IsRequired = true)]
        public StringValueObject Token { get; private set; }

        [Parameter(DisplayOrder = 3, IsRequired = true)]
        public StringValueObject UserKey { get; private set; }

        [Parameter(DisplayOrder = 4, IsRequired = true)]
        public StringValueObject Message { get; private set; }

        [Input(DisplayOrder = 5, InitOrder = 2)]
        public IList<StringValueObject> Variables { get; private set; }

        [Input(DisplayOrder = 6, InitOrder = 1, IsDefaultShown = false)]
        public IntValueObject VariableCount { get; private set; }

        [Input(DisplayOrder = 7, InitOrder = 3)]
        public StringValueObject Attachment { get; private set; }

        public override void Execute()
        {
            if (!this.Trigger.HasValue || !this.Trigger.WasSet || !this.Trigger.Value) return;

            var parameters = new NameValueCollection {
                { "token", this.Token },
                { "user", this.UserKey },
                { "message", this.ComposeMessage() }
            };

            using (var client = new WebClient())
            {
                // client.UploadValues("https://api.pushover.net/1/messages.json", parameters);
                HttpUploadFile("https://api.pushover.net/1/messages.json", this.Attachment, "image.jpeg", "attachment", "image/jpeg", parameters);
            }
        }

        private string ComposeMessage()
        {
            try
            {
                return string.Format(this.Message, this.Variables.Select(v => v.Value).ToArray());
            }
            catch (FormatException)
            {
                return this.Message;
            }
        }

        private void SetupIgnoreSSLTrust()
        {
            ServicePointManager.ServerCertificateValidationCallback =
                new RemoteCertificateValidationCallback(
                    delegate
                    { return true; }
                );
        }


        public static void HttpUploadFile(string url, string file, string fileName, string paramName, string contentType, NameValueCollection nvc)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = System.Net.CredentialCache.DefaultCredentials;
            Stream rs = wr.GetRequestStream();
            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in nvc.Keys)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formdataTemplate, key, nvc[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            rs.Write(boundarybytes, 0, boundarybytes.Length);
            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
            string header = string.Format(headerTemplate, paramName, fileName, contentType);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);

            using (WebClient fileClient = new WebClient())
            {
                byte[] buffer = fileClient.DownloadData(file);
                rs.Write(buffer, 0, buffer.Length);
                fileClient.Dispose();
            }            

            byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();
            WebResponse wresp = null;
            try
            {
                wresp = wr.GetResponse();
                Stream stream2 = wresp.GetResponseStream();
                StreamReader reader2 = new StreamReader(stream2);
                var result = reader2.ReadToEnd();
            }
            catch (Exception ex)
            {
                // System.Windows.MessageBox.Show("Error occurred while converting file", "Error!");
                if (wresp != null)
                {
                    wresp.Close();
                    wresp = null;
                }
            }
            finally
            {
                wr = null;
            }
        }
    }
}
