﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Security;
using ContentByMail.Common.Enumerations;
using ContentByMail.Core.Notifications;
using ContentByMail.Common;
using ContentByMail.Core.EmailProcessor;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Security.Accounts;
using Constants = ContentByMail.Common.Constants;
using Sitecore.Diagnostics;

namespace ContentByMail.Pipelines.ContentByMail.ProcessEmail
{
   

    public class SimpleEmailMessageProcessor : IEmailMessageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleEmailMessageProcessor"/> class.
        /// </summary>
        public SimpleEmailMessageProcessor() { }

        /// <summary>
        /// Processes the specified message.
        /// </summary>
        /// <param name="args">The args.</param>
        public void Process(PostmarkMessageArgs args)
        {
            try
            {
                Assert.ArgumentNotNull(args, "args");
                Assert.IsNotNull(args.Message, "args.Message");


                var template = args.MessageTokenValues["Template"];

                var contentEmailModule = Context.ContentDatabase.GetItem(Constants.Settings.ContentByEmailModuleItem);

                IEnumerable<EmailProcessorTemplate> emailProcessorTemplates = EmailProcessorTemplateFactory.CreateCollection();


                EmailProcessorTemplate emailProcessorTemplate = emailProcessorTemplates.FirstOrDefault(emailProcessor => emailProcessor.EmailTemplateName == template);

                Assert.IsNotNull(emailProcessorTemplate, String.Format("{0} processorTemplate", template));

            

                if (emailProcessorTemplate != null)
                {
                    Item parentFolder = emailProcessorTemplate.FolderTemplateToInsertCreatedItemIn;
                    TemplateItem newItemTemplateId = emailProcessorTemplate.ItemTemplateToCreateItemFrom;
                    bool createAsuser = emailProcessorTemplate.CreateAsuser;
                    bool autoProcessFields = emailProcessorTemplate.AutoProcessFields;
                
                    User account = null;

                    List<string> missingFieldFlag = new List<string>();
                    Dictionary<string, string> tokenFieldList = new Dictionary<string, string>();

                    if (parentFolder != null)
                    {
                    
                        if (createAsuser)
                        {
                            var username = Membership.GetUserNameByEmail(args.Message.From);
                            if (!string.IsNullOrEmpty(username) && User.Exists(username))
                            {
                                account = User.FromName(username, true);
                            }
                            else
                            {
                                account = Constants.Security.ServiceUser;
                            }
                        }

                        using (new UserSwitcher(account))
                        {
                            parentFolder.Editing.BeginEdit();

                            Item newItem = parentFolder.Add(ItemUtil.ProposeValidItemName(args.Message.Subject), newItemTemplateId);

                            if (autoProcessFields)
                            {
                                foreach (var messageTokenValue in args.MessageTokenValues)
                                {
                                    if (newItem.Fields[messageTokenValue.Key] == null)
                                    {
                                        missingFieldFlag.Add(messageTokenValue.Key);
                                    }

                                    newItem[messageTokenValue.Key] = messageTokenValue.Value;
                                }
                            }
                            else
                            {
                               

                                foreach (EmailProcessorTemplateToken token in emailProcessorTemplate.EmailTokens)
                                {
                                    if (args.MessageTokenValues.ContainsKey(token.CustomField))
                                        newItem[token.CustomField] = args.MessageTokenValues[token.SitecoreField];
                                }
                            }                        

                            parentFolder.Editing.EndEdit();
                        }                    
                    }

                    NotificationMessageFactory factory = new NotificationMessageFactory();
                    NotificationMessage notificationMessage = factory.CreateMessage(emailProcessorTemplate.NotificationTemplate.ID);
                    NotificationManager manager = new NotificationManager();
                    NotificationMessageType type;

                    if (missingFieldFlag.Count > 0)
                    {
                        type = NotificationMessageType.InvalidField;
                    }
                    else
                    {
                        type = NotificationMessageType.Success;

                    }

                    manager.Send(args.Message.From, Constants.DefaultContentModule.DefaultMessage, type);

                    Log.Info("In SimpleEmailMessageProcessor", this);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Process", ex, this);
            }                                  
        }
    }
}
