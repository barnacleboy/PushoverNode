using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Security;
using LogicModule.Nodes.Helpers;
using LogicModule.ObjectModel;
using LogicModule.ObjectModel.TypeSystem;

namespace PushoverNode
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
                client.UploadValues("https://api.pushover.net/1/messages.json", parameters);
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
    }
}
