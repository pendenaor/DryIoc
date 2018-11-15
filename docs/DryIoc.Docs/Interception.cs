/*md
<!--Auto-generated from .cs file, the edits here will be lost! -->

# Interception

[TOC]

## Decorator with Castle DynamicProxy

Decorator pattern is a good fit for implementing [cross-cutting concerns](https://en.wikipedia.org/wiki/Cross-cutting_concern).

But we can extend it further to implement [AOP Interception](https://en.wikipedia.org/wiki/Aspect-oriented_programming) 
with help of [Castle DynamicProxy](http://www.castleproject.org/projects/dynamicproxy/).

Let's define an extension method for intercepting interfaces and classes:

```cs md*/

using DryIoc;
using ImTools;
using Castle.DynamicProxy;
using IInterceptor = Castle.DynamicProxy.IInterceptor;
using LinFu.DynamicProxy;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Reflection;

public static class DryIocInterception
{
    private static readonly DefaultProxyBuilder _proxyBuilder = new DefaultProxyBuilder();

    public static void Intercept<TService, TInterceptor>(this IRegistrator registrator, object serviceKey = null) 
        where TInterceptor : class, IInterceptor
    {
        var serviceType = typeof(TService);

        Type proxyType;
        if (serviceType.IsInterface())
            proxyType = _proxyBuilder.CreateInterfaceProxyTypeWithTargetInterface(
                serviceType, ArrayTools.Empty<Type>(), ProxyGenerationOptions.Default);
        else if (serviceType.IsClass())
            proxyType = _proxyBuilder.CreateClassProxyTypeWithTarget(
                serviceType, ArrayTools.Empty<Type>(), ProxyGenerationOptions.Default);
        else
            throw new ArgumentException(
                $"Intercepted service type {serviceType} is not a supported, cause it is nor a class nor an interface");

        registrator.Register(serviceType, proxyType,
            made: Made.Of(pt => pt.PublicConstructors().FindFirst(ctor => ctor.GetParameters().Length != 0),
                Parameters.Of.Type<IInterceptor[]>(typeof(TInterceptor[]))),
            setup: Setup.DecoratorOf(useDecorateeReuse: true, decorateeServiceKey: serviceKey));
    }
} /*md
```

Now define a method interceptor:
```cs md*/
public class FooLoggingInterceptor : IInterceptor
{
    public List<string> LogLines = new List<string>();
    private void Log(string line) => LogLines.Add(line);

    public void Intercept(IInvocation invocation)
    {
        Log($"Invoking method: {invocation.GetConcreteMethod().Name}");
        invocation.Proceed();
    }
}/*md
```

Register service and its interceptor as a normal services, then link them together via Intercept method:
```cs md*/

public class Register_and_use_interceptor
{
    public interface IFoo
    {
        void Greet();
    }

    public class Foo : IFoo
    {
        public void Greet() { }
    }

    [Test]
    public void Example()
    {
        var container = new Container();
        container.Register<IFoo, Foo>();
        container.Register<FooLoggingInterceptor>(Reuse.Singleton);
        container.Intercept<IFoo, FooLoggingInterceptor>();

        var foo = container.Resolve<IFoo>();
        foo.Greet();

        // examine that logging indeed was hooked up
        var logger = container.Resolve<FooLoggingInterceptor>();
        Assert.AreEqual("Invoking method: Greet", logger.LogLines[0]);
    }
}/*md
```


## Decorator with LinFu DynamicProxy

Lately, there was a new release of [LinFu.DynamicProxy]() with .NET Standard 2.0 support.

It is interesting to at least have an alternative to Castle.DynamicProxy. Let's try it out :-)

```cs md*/
public static class DryIocInterceptionLinFu
{
    private static readonly ProxyFactory _proxyFactory = new ProxyFactory();

    private static readonly MethodInfo _createProxyMethod = typeof(ProxyFactory)
        .Method(nameof(ProxyFactory.CreateProxy), typeof(IInvokeWrapper), typeof(Type[]));

    public static void InterceptInvocation<TService, TInvokeWrapper>(this IRegistrator registrator, object serviceKey = null)
        where TInvokeWrapper : class, IInvokeWrapper
    {
        var serviceType = typeof(TService);
        if (!serviceType.IsAbstract())
            throw new ArgumentException($"Non-abstract {serviceType} are not a supported.");

        var createProxyMethod = _createProxyMethod.MakeGenericMethod(serviceType);

        registrator.Register(serviceType, 
            made: Made.Of(FactoryMethod.Of(createProxyMethod, _proxyFactory),
                Parameters.Of.Type<IInvokeWrapper>(typeof(TInvokeWrapper)).Type<Type[]>(_ => null)),
            setup: Setup.DecoratorOf(useDecorateeReuse: true, decorateeServiceKey: serviceKey));
    }
}

// Simplify implementation of wrapper
public abstract class InvokeWrapperBase<TIntercepted> : IInvokeWrapper
{
    public readonly TIntercepted Target;
    protected InvokeWrapperBase(TIntercepted target)
    {
        Target = target;
    }
    public virtual void BeforeInvoke(InvocationInfo info) { }
    public virtual void AfterInvoke(InvocationInfo info, object returnValue) { }
    public virtual object DoInvoke(InvocationInfo info) => info.TargetMethod.Invoke(Target, info.Arguments);
}

/*md
```
Implement `IInvokeWrapper` to intercept the methods of `IBar` instances.
Then register service, `BarInvokeWrapper`, and link them together via Intercept method:
```cs md*/

public class Register_and_use_interceptor_with_LinFu
{
    public interface IBar
    {
        void Greet();
    }
    public class Bar : IBar
    {
        public void Greet() { }
    }

    public class BarLogger
    {
        public List<string> LogLines = new List<string>();
        public void Log(string line) => LogLines.Add(line);
    }

    public class BarInvokeWrapper : InvokeWrapperBase<IBar>
    {
        private readonly BarLogger _logger;
        public BarInvokeWrapper(IBar bar, BarLogger logger) : base(bar)
        {
            _logger = logger;
        }

        public override object DoInvoke(InvocationInfo info)
        {
            _logger.Log($"Invoking method: {info.TargetMethod.Name}");
            // may be optimized, because we know the actual `Target` object here.
            //if (info.TargetMethod.Name == nameof(IBar.Greet))
            //    Target.Greet();
            return base.DoInvoke(info);
        }
    }

    [Test]
    public void Example()
    {
        var container = new Container();

        container.Register<IBar, Bar>();
        container.Register<BarLogger>(Reuse.Singleton);
        container.Register<BarInvokeWrapper>();
        container.InterceptInvocation<IBar, BarInvokeWrapper>();

        var foo = container.Resolve<IBar>();
        foo.Greet();

        // examine that logging indeed was hooked up
        var logger = container.Resolve<BarLogger>();
        Assert.AreEqual("Invoking method: Greet", logger.LogLines[0]);
    }
}/*md
```
md*/
