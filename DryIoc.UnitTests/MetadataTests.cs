using System;
using System.Linq;
using DryIoc.UnitTests.CUT;
using NUnit.Framework;

namespace DryIoc.UnitTests
{
    [TestFixture]
    public class MetadataTests
    {
        [Test]
        public void I_can_resolve_transient_service_with_metadata()
        {
            var container = new Container();
            container.Register(typeof(ServiceWithMetadata), with: new FactoryOptions(metadata: new Metadata { Assigned = true }));

            var service = container.Resolve<Meta<ServiceWithMetadata, Metadata>>();

            Assert.That(service.Metadata.Assigned, Is.True);
        }

        [Test]
        public void I_can_resolve_func_of_transient_service_with_metadata()
        {
            var container = new Container();
            container.Register(typeof(ServiceWithMetadata), with: new FactoryOptions(metadata: new Metadata { Assigned = true }));

            var func = container.Resolve<Meta<Func<ServiceWithMetadata>, Metadata>>();

            Assert.That(func.Metadata.Assigned, Is.True);
            Assert.That(func.Value(), Is.Not.Null);
        }

        [Test]
        public void I_can_resolve_array_of_func_of_transient_service_with_metadata()
        {
            var container = new Container();
            container.Register(typeof(ServiceWithMetadata), with: new FactoryOptions(metadata: new Metadata { Assigned = true }));

            var funcs = container.Resolve<Meta<Func<ServiceWithMetadata>, Metadata>[]>();

            Assert.That(funcs.Length, Is.EqualTo(1));
            Assert.That(funcs[0].Metadata.Assigned, Is.True);
            Assert.That(funcs[0].Value(), Is.Not.Null);
        }

        [Test]
        public void I_can_resolve_array_of_func_of_transient_services_with_metadata()
        {
            var container = new Container();
            container.Register(typeof(IService), typeof(Service), with: new FactoryOptions(metadata: "One"));
            container.Register(typeof(IService), typeof(AnotherService), with: new FactoryOptions(metadata: "Another"));

            var funcs = container.Resolve<Meta<Func<IService>, string>[]>();
            Assert.That(funcs.Length, Is.EqualTo(2));

            var func = funcs.First(x => x.Metadata.Equals("Another"));
            Assert.That(func.Value(), Is.InstanceOf<AnotherService>());
        }

        [Test]
        public void I_can_resolve_singleton_service_with_metadata()
        {
            var container = new Container();
            container.Register<ServiceWithMetadata>(Reuse.Singleton, with: new FactoryOptions(metadata: new Metadata { Assigned = true }));

            var meta = container.Resolve<Meta<Func<ServiceWithMetadata>, Metadata>>();
            Assert.That(meta.Metadata.Assigned, Is.True);

            var anotherService = container.Resolve<Meta<ServiceWithMetadata, Metadata>>();
            Assert.That(meta.Value(), Is.SameAs(anotherService.Value));
        }

        [Test]
        public void I_can_resolve_lazy_service_with_metadata()
        {
            var container = new Container();
            container.Register<ServiceWithMetadata>(Reuse.Singleton, with: new FactoryOptions(metadata: new Metadata { Assigned = true }));

            var meta = container.Resolve<Meta<Lazy<ServiceWithMetadata>, Metadata>>();

            Assert.That(meta.Metadata.Assigned, Is.True);
            Assert.That(meta.Value.Value, Is.InstanceOf<ServiceWithMetadata>());
        }

        [Test]
        public void I_can_resolve_array_of_lazy_with_metadata()
        {
            var container = new Container();
            container.Register<ServiceWithMetadata>(Reuse.Singleton, with: new FactoryOptions(metadata: new Metadata { Assigned = true }));

            // Then
            var metas = container.Resolve<Meta<Lazy<ServiceWithMetadata>, Metadata>[]>();

            Assert.That(metas.Length, Is.EqualTo(1));
            Assert.That(metas[0].Metadata.Assigned, Is.True);
        }

        [Test]
        public void When_singleton_resolve_through_meta_lazy_It_should_not_be_instantiated()
        {
            var container = new Container();
            ServiceWithInstanceCount.InstanceCount = 0;
            container.Register<ServiceWithInstanceCount>(Reuse.Singleton, with: new FactoryOptions(metadata: "hey"));

            container.Resolve<Meta<Lazy<ServiceWithInstanceCount>, string>>();

            Assert.That(ServiceWithInstanceCount.InstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void I_can_resolve_open_generic_with_meta_array_dependency()
        {
            var container = new Container();
            container.Register(typeof(ServiceWithMetadata), with: new FactoryOptions(metadata: new Metadata { Assigned = true }));
            container.Register(typeof(MetadataDrivenFactory<,>));

            var factory = container.Resolve<MetadataDrivenFactory<ServiceWithMetadata, Metadata>>();
            var service = factory.CreateOnlyIf(metadata => metadata.Assigned);

            Assert.That(service, Is.InstanceOf<ServiceWithMetadata>());
        }

        [Test]
        public void I_can_resolve_service_with_dependency_on_open_generic_with_meta_array_dependency()
        {
            var container = new Container();
            container.Register(typeof(ServiceWithDependencyOnOpenGenericWithMetaFactoryMany));
            container.Register(typeof(ServiceWithMetadata), with: new FactoryOptions(metadata: new Metadata { Assigned = true }));
            container.Register(typeof(MetadataDrivenFactory<,>));

            var service = container.Resolve<ServiceWithDependencyOnOpenGenericWithMetaFactoryMany>();
            var dependency = service.Factory.CreateOnlyIf(metadata => metadata.Assigned);

            Assert.That(dependency, Is.InstanceOf<ServiceWithMetadata>());
        }

        [Test]
        public void Resolve_should_throw_if_metadata_is_not_registered()
        {
            var container = new Container();

            container.Register(typeof(IService), typeof(Service));

            Assert.Throws<ContainerException>(
                () => container.Resolve<Meta<IService, Metadata>>());
        }

        [Test]
        public void Resolve_should_throw_if_requested_metadata_is_of_different_type()
        {
            var container = new Container();

            container.Register(typeof(IService), typeof(Service), named: "oh my!");

            Assert.Throws<ContainerException>(
                () => container.Resolve<Meta<IService, Metadata>>());
        }

        [Test]
        public void When_one_service_is_registered_with_metadata_and_another_without_Resolved_array_should_contain_only_one()
        {
            var container = new Container();

            container.Register(typeof(IService), typeof(Service), with: new FactoryOptions(metadata: "xx"));
            container.Register(typeof(IService), typeof(Service));

            var services = container.Resolve<Meta<IService, string>[]>();

            Assert.That(services.Length, Is.EqualTo(1));
            Assert.That(services.Single().Metadata, Is.EqualTo("xx"));
        }

        [Test]
        public void Should_resolve_open_generic_with_metadata()
        {
            var container = new Container();
            container.Register(typeof(IService<>), typeof(Service<>), with: new FactoryOptions(metadata: "ho"));

            var service = container.Resolve<Meta<IService<int>, string>>();

            Assert.That(service.Value, Is.InstanceOf<Service<int>>());
        }

        [Test]
        public void Should_NOT_resolve_meta_with_name_if_no_such_name_registered()
        {
            var container = new Container();
            container.RegisterPublicTypes(typeof(Service<>), with: new FactoryOptions(metadata: 3));

            var ex = Assert.Throws<ContainerException>(() =>
                container.Resolve<Meta<IService<object>, int>>("no-no-name"));
            Assert.That(ex.Message, Is.StringStarting("Unable to resolve"));
        }

        [Test]
        public void Should_resolve_any_named_service_with_corresponding_metadata_If_name_is_not_specified_in_resolve()
        {
            var container = new Container();
            container.RegisterPublicTypes(typeof(Service<>), with: new FactoryOptions(metadata: 3), named: "some");

            var meta = container.Resolve<Meta<IService<string>, int>>();

            Assert.That(meta.Metadata, Is.EqualTo(3));
        }

        [Test]
        public void When_one_service_is_registered_with_name_and_other_is_default_only_one_named_should_be_resolved()
        {
            var container = new Container();

            container.Register<IService, Service>(named: "n", with: new FactoryOptions(metadata: "m"));
            container.Register<IService, Service>();

            var services = container.Resolve<Meta<IService, string>[]>();

            Assert.That(services.Length, Is.EqualTo(1));
            Assert.That(services.Single().Metadata, Is.EqualTo("m"));
        }
    }

    #region CUT

    public class ServiceWithMetadata
    {
    }

    public class Metadata
    {
        public bool Assigned;
    }

    public class MetadataDrivenFactory<TService, TMetadata>
    {
        private readonly Meta<Func<TService>, TMetadata>[] _factories;

        public MetadataDrivenFactory(Meta<Func<TService>, TMetadata>[] factories)
        {
            _factories = factories;
        }

        public TService CreateOnlyIf(Func<TMetadata, bool> condition)
        {
            var factory = _factories.First(meta => condition(meta.Metadata));
            return factory.Value();
        }
    }

    public class ServiceWithDependencyOnOpenGenericWithMetaFactoryMany
    {
        public MetadataDrivenFactory<ServiceWithMetadata, Metadata> Factory { get; set; }

        public ServiceWithDependencyOnOpenGenericWithMetaFactoryMany(MetadataDrivenFactory<ServiceWithMetadata, Metadata> factory)
        {
            Factory = factory;
        }
    }

    #endregion
}