﻿using System.ComponentModel.Composition;
using Microsoft.Practices.Prism.MefExtensions.Modularity;
using Microsoft.Practices.Prism.Regions;
using Samba.Domain.Models.Accounts;
using Samba.Localization.Properties;
using Samba.Modules.AccountModule.Dashboard;
using Samba.Presentation.Common;
using Samba.Presentation.Common.ModelBase;
using Samba.Services;
using Samba.Services.Common;

namespace Samba.Modules.AccountModule
{
    [ModuleExport(typeof(AccountModule))]
    public class AccountModule : VisibleModuleBase
    {
        private readonly IRegionManager _regionManager;
        private readonly IUserService _userService;
        private readonly AccountSelectorView _accountSelectorView;
        private readonly AccountSelectorViewModel _accountSelectorViewModel;
        private readonly AccountDetailsView _accountDetailsView;
        private readonly DocumentCreatorView _documentCreatorView;
        private readonly BatchDocumentCreatorView _batchDocumentCreatorView;
        private readonly BatchDocumentCreatorViewModel _batchDocumentCreatorViewModel;

        [ImportingConstructor]
        public AccountModule(IRegionManager regionManager,
            IUserService userService,
            AccountSelectorView accountSelectorView, AccountSelectorViewModel accountSelectorViewModel,
            AccountDetailsView accountDetailsView,
            DocumentCreatorView documentCreatorView,
            BatchDocumentCreatorView batchDocumentCreatorView,BatchDocumentCreatorViewModel batchDocumentCreatorViewModel)
            : base(regionManager, AppScreens.AccountList)
        {
            _regionManager = regionManager;
            _userService = userService;
            _accountSelectorView = accountSelectorView;
            _accountSelectorViewModel = accountSelectorViewModel;
            _accountDetailsView = accountDetailsView;
            _documentCreatorView = documentCreatorView;
            _batchDocumentCreatorView = batchDocumentCreatorView;
            _batchDocumentCreatorViewModel = batchDocumentCreatorViewModel;

            AddDashboardCommand<EntityCollectionViewModelBase<AccountTemplateViewModel, AccountTemplate>>(Resources.AccountTemplate.ToPlural(), Resources.Accounts, 40);
            AddDashboardCommand<EntityCollectionViewModelBase<AccountViewModel, Account>>(Resources.Account.ToPlural(), Resources.Accounts, 40);
            AddDashboardCommand<EntityCollectionViewModelBase<AccountScreenViewModel, AccountScreen>>(Resources.AccountScreen.ToPlural(), Resources.Accounts, 40);
            AddDashboardCommand<EntityCollectionViewModelBase<AccountTransactionTemplateViewModel, AccountTransactionTemplate>>(Resources.TransactionTemplate.ToPlural(), Resources.Accounts, 40);
            AddDashboardCommand<EntityCollectionViewModelBase<AccountTransactionDocumentViewModel, AccountTransactionDocument>>(Resources.TransactionDocument.ToPlural(), Resources.Accounts, 40);
            AddDashboardCommand<EntityCollectionViewModelBase<AccountTransactionDocumentTemplateViewModel, AccountTransactionDocumentTemplate>>(Resources.DocumentTemplate.ToPlural(), Resources.Accounts, 40);

            PermissionRegistry.RegisterPermission(PermissionNames.NavigateAccountView, PermissionCategories.Navigation, Resources.CanNavigateCash);
            PermissionRegistry.RegisterPermission(PermissionNames.MakeAccountTransaction, PermissionCategories.Cash, Resources.CanMakeAccountTransaction);
            PermissionRegistry.RegisterPermission(PermissionNames.CreateAccount, PermissionCategories.Account, Resources.CanCreateAccount);

            SetNavigationCommand(Resources.Accounts, Resources.Common, "Images/Xls.png", 70);
        }

        protected override void OnInitialization()
        {
            _regionManager.RegisterViewWithRegion(RegionNames.MainRegion, typeof(AccountSelectorView));
            _regionManager.RegisterViewWithRegion(RegionNames.MainRegion, typeof(AccountDetailsView));
            _regionManager.RegisterViewWithRegion(RegionNames.MainRegion, typeof(DocumentCreatorView));
            _regionManager.RegisterViewWithRegion(RegionNames.MainRegion, typeof(BatchDocumentCreatorView));

            EventServiceFactory.EventService.GetEvent<GenericEvent<AccountTransactionDocumentTemplate>>().Subscribe(
                x =>
                {
                    if (x.Topic == EventTopicNames.BatchCreateDocument)
                    {
                        _batchDocumentCreatorViewModel.Update(x.Value);
                        ActivateBatchDocumentCreator();
                    }
                    if (x.Topic == EventTopicNames.BatchDocumentsCreated)
                    {
                        ActivateAccountSelector();
                    }
                });

            EventServiceFactory.EventService.GetEvent<GenericEvent<DocumentCreationData>>().Subscribe(x =>
                {
                    if (x.Topic == EventTopicNames.AccountTransactionDocumentSelected)
                    {
                        ActivateDocumentCreator();
                    }
                });

            EventServiceFactory.EventService.GetEvent<GenericEvent<EntityOperationRequest<AccountData>>>().Subscribe(
                x =>
                {
                    if (x.Topic == EventTopicNames.DisplayAccountTransactions)
                    {
                        ActivateAccountTransactions();
                    }
                    if (x.Topic == EventTopicNames.ActivateAccountSelector)
                    {
                        ActivateAccountSelector();
                    }
                });
        }

        private void ActivateBatchDocumentCreator()
        {
            _regionManager.Regions[RegionNames.MainRegion].Activate(_batchDocumentCreatorView);
        }

        private void ActivateDocumentCreator()
        {
            _regionManager.Regions[RegionNames.MainRegion].Activate(_documentCreatorView);
        }

        private void ActivateAccountTransactions()
        {
            _regionManager.Regions[RegionNames.MainRegion].Activate(_accountDetailsView);
        }

        private void ActivateAccountSelector()
        {
            _regionManager.Regions[RegionNames.MainRegion].Activate(_accountSelectorView);
        }

        public override object GetVisibleView()
        {
            return _accountSelectorView;
        }

        protected override bool CanNavigate(string arg)
        {
            return _userService.IsUserPermittedFor(PermissionNames.NavigateAccountView);
        }

        protected override void OnNavigate(string obj)
        {
            Activate();
            _accountSelectorViewModel.Refresh();
            base.OnNavigate(obj);
        }
    }
}
