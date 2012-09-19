﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Linq.Expressions;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Tickets;
using Samba.Infrastructure.Settings;
using Samba.Localization.Properties;
using Samba.Persistance.Data;
using Samba.Persistance.Data.Specification;
using Samba.Presentation.Common;
using Samba.Services;
using Samba.Services.Common;

namespace Samba.Modules.AccountModule
{
    [Export]
    public class AccountDetailsViewModel : ObservableObject
    {
        private readonly IApplicationState _applicationState;
        private readonly ICacheService _cacheService;
        private readonly IAccountService _accountService;

        [ImportingConstructor]
        public AccountDetailsViewModel(IApplicationState applicationState, ICacheService cacheService, IAccountService accountService)
        {
            _applicationState = applicationState;
            _cacheService = cacheService;
            _accountService = accountService;
            CloseAccountScreenCommand = new CaptionCommand<string>(Resources.Close, OnCloseAccountScreen);
            DisplayTicketCommand = new CaptionCommand<string>(Resources.FindTicket, OnDisplayTicket);
            AccountDetails = new ObservableCollection<AccountDetailViewModel>();
            DocumentTemplates = new ObservableCollection<DocumentTemplateButtonViewModel>();
            AccountSummaries = new ObservableCollection<AccountSummaryViewModel>();
            EventServiceFactory.EventService.GetEvent<GenericEvent<EntityOperationRequest<AccountData>>>().Subscribe(OnDisplayAccountTransactions);
        }

        public AccountTemplate SelectedAccountTemplate { get; set; }
        public AccountDetailViewModel FocusedAccountTransaction { get; set; }

        private Account _selectedAccount;
        public Account SelectedAccount
        {
            get { return _selectedAccount; }
            set
            {
                _selectedAccount = value;
                if (SelectedAccountTemplate == null || SelectedAccountTemplate.Id != _selectedAccount.AccountTemplateId)
                {
                    SelectedAccountTemplate = _cacheService.GetAccountTemplateById(value.AccountTemplateId);
                }
                RaisePropertyChanged(() => SelectedAccount);
                FilterType = FilterTypes[SelectedAccountTemplate.DefaultFilterType];
                UpdateTemplates();
            }
        }

        public ObservableCollection<DocumentTemplateButtonViewModel> DocumentTemplates { get; set; }
        public ObservableCollection<AccountDetailViewModel> AccountDetails { get; set; }
        public ObservableCollection<AccountSummaryViewModel> AccountSummaries { get; set; }

        public string[] FilterTypes { get { return new[] { Resources.All, Resources.Month, Resources.Week, Resources.WorkPeriod }; } }

        private string _filterType;
        private EntityOperationRequest<AccountData> _currentOperationRequest;

        public string FilterType
        {
            get { return _filterType; }
            set
            {
                _filterType = value;
                DisplayTransactions();
            }
        }

        public string TotalBalance { get { return AccountDetails.Sum(x => x.Debit - x.Credit).ToString(LocalSettings.DefaultCurrencyFormat); } }

        public ICaptionCommand CloseAccountScreenCommand { get; set; }
        public ICaptionCommand DisplayTicketCommand { get; set; }

        private void UpdateTemplates()
        {
            DocumentTemplates.Clear();
            if (SelectedAccount != null)
            {
                var templates = _cacheService.GetAccountTransactionDocumentTemplates(SelectedAccount.AccountTemplateId);
                DocumentTemplates.AddRange(templates.Select(x => new DocumentTemplateButtonViewModel(x, SelectedAccount)));
            }
        }

        private Expression<Func<AccountTransactionValue, bool>> GetCurrentRange(Expression<Func<AccountTransactionValue, bool>> activeSpecification)
        {
            if (FilterType == Resources.Month) return activeSpecification.And(x => x.Date >= DateTime.Now.MonthStart());
            if (FilterType == Resources.WorkPeriod) return activeSpecification.And(x => x.Date >= _applicationState.CurrentWorkPeriod.StartDate);
            return activeSpecification;
        }

        private Expression<Func<AccountTransactionValue, bool>> GetPastRange(Expression<Func<AccountTransactionValue, bool>> activeSpecification)
        {
            if (FilterType == Resources.Month) return activeSpecification.And(x => x.Date < DateTime.Now.MonthStart());
            if (FilterType == Resources.WorkPeriod) return activeSpecification.And(x => x.Date < _applicationState.CurrentWorkPeriod.StartDate);
            return activeSpecification;
        }

        private void DisplayTransactions()
        {
            AccountDetails.Clear();
            AccountSummaries.Clear();

            var transactions = Dao.Query(GetCurrentRange(x => x.AccountId == SelectedAccount.Id)).OrderBy(x => x.Date);
            AccountDetails.AddRange(transactions.Select(x => new AccountDetailViewModel(x)));

            if (FilterType != Resources.All)
            {
                var pastDebit = Dao.Sum(x => x.Debit, GetPastRange(x => x.AccountId == SelectedAccount.Id));
                var pastCredit = Dao.Sum(x => x.Credit, GetPastRange(x => x.AccountId == SelectedAccount.Id));
                if (pastCredit > 0 || pastDebit > 0)
                {
                    AccountSummaries.Add(new AccountSummaryViewModel(Resources.Total, AccountDetails.Sum(x => x.Debit), AccountDetails.Sum(x => x.Credit)));
                    var detailValue =
                        new AccountDetailViewModel(new AccountTransactionValue
                                                       {
                                                           Name = Resources.PastTransactions,
                                                           Credit = pastCredit,
                                                           Debit = pastDebit
                                                       });
                    AccountDetails.Insert(0, detailValue);
                    detailValue.IsBold = true;
                }
            }

            AccountSummaries.Add(new AccountSummaryViewModel(Resources.GrandTotal, AccountDetails.Sum(x => x.Debit), AccountDetails.Sum(x => x.Credit)));

            for (var i = 0; i < AccountDetails.Count; i++)
            {
                AccountDetails[i].Balance = (AccountDetails[i].Debit - AccountDetails[i].Credit);
                if (i > 0) (AccountDetails[i].Balance) += (AccountDetails[i - 1].Balance);
            }

            RaisePropertyChanged(() => TotalBalance);
        }

        private void OnDisplayAccountTransactions(EventParameters<EntityOperationRequest<AccountData>> obj)
        {
            if (obj.Topic == EventTopicNames.DisplayAccountTransactions)
            {
                var account = _accountService.GetAccountById(obj.Value.SelectedEntity.AccountId);
                if (obj.Value != null && !string.IsNullOrEmpty(obj.Value.GetExpectedEvent()))
                    _currentOperationRequest = obj.Value;
                SelectedAccount = account;//= new ResourceSearchResultViewModel(obj.Value.SelectedEntity, _cacheService.GetResourceTemplateById(obj.Value.SelectedEntity.AccountTemplateId));
            }
        }

        private void OnCloseAccountScreen(string obj)
        {
            AccountDetails.Clear();
            if (_currentOperationRequest != null)
                _currentOperationRequest.Publish(new AccountData { AccountId = SelectedAccount.Id });
        }

        private void OnDisplayTicket(string obj)
        {
            if (FocusedAccountTransaction != null)
            {
                var did = FocusedAccountTransaction.Model.AccountTransactionDocumentId;
                var ticket = Dao.Single<Ticket>(x => x.TransactionDocument.Id == did);
                if (ticket != null)
                    ExtensionMethods.PublishIdEvent(ticket.Id, EventTopicNames.DisplayTicket);
            }
        }
   }
}
