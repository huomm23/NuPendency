﻿using Ninject;
using Ninject.Parameters;
using NuPendency.Commons.Interfaces;
using NuPendency.Commons.Ui;
using NuPendency.Gui.ViewModels;
using System;
using IInitializable = NuPendency.Commons.Interfaces.IInitializable;

namespace NuPendency.Gui
{
    public class ViewModelLocator : IViewModelFactory, IInstanceCreator
    {
        protected readonly IKernel Kernel;

        private static ViewModelLocator s_Instance;

        public ViewModelLocator()
        {
            Kernel.Bind<MainWindowViewModel>().To<MainWindowViewModel>().InSingletonScope();
        }

        public ViewModelLocator(IKernel kernel)
        {
            Kernel = kernel;
            kernel.Bind<IViewModelFactory, IInstanceCreator>().ToConstant(this);

            Kernel.Bind<MainWindowViewModel>().To<MainWindowViewModel>().InSingletonScope();
        }

        public static IInstanceCreator DesignInstanceCreator => s_Instance ?? (s_Instance = new ViewModelLocator());

        public static IViewModelFactory DesignViewModelFactory => s_Instance ?? (s_Instance = new ViewModelLocator());

        public MainWindowViewModel MainWindowViewModel => Kernel.Get<MainWindowViewModel>();

        public T Create<T>()
        {
            var newObject = Kernel.Get<T>();
            InitializeInitialziable(newObject as IInitializable);
            return newObject;
        }

        public InputBoxViewModel CreateInputBoxViewModel(string text, Action<string> okAction, Action<string> cancelAction)
        {
            return CreateInstance<InputBoxViewModel>(new[]
            {
                new ConstructorArgument(@"text", text),
                new ConstructorArgument(@"okAction", okAction),
                new ConstructorArgument(@"cancelAction", cancelAction),
            });
        }

        public T CreateInstance<T>(ConstructorArgument[] arguments)
        {
            var vm = Kernel.Get<T>(arguments);
            InitializeInitialziable(vm as IInitializable);
            return vm;
        }

        public TVm CreateViewModel<T, TVm>(T model)
        {
            var vm = Kernel.Get<TVm>(new ConstructorArgument(@"model", model));
            InitializeInitialziable(vm as IInitializable);
            return vm;
        }

        public TVm CreateViewModel<TVm>()
        {
            var vm = Kernel.Get<TVm>();
            InitializeInitialziable(vm as IInitializable);
            return vm;
        }

        private static void InitializeInitialziable(IInitializable initializable)
        {
            initializable?.Init();
        }
    }
}