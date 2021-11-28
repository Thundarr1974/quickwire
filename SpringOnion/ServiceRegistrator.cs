﻿using Microsoft.Extensions.DependencyInjection;
using SpringOnion.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SpringOnion
{
    public static class ServiceRegistrator
    {
        public static IEnumerable<ServiceDescriptor> GetServiceDescriptors(Type type, string environmentName)
        {
            if (EnvironmentSelectorAttribute.IsEnabled(type.GetCustomAttribute<EnvironmentSelectorAttribute>(), environmentName))
            {
                foreach (RegisterServiceAttribute registerAttribute in type.GetCustomAttributes<RegisterServiceAttribute>())
                {
                    Type serviceType = registerAttribute.ServiceType ?? type;
                    yield return new ServiceDescriptor(serviceType, GetFactory(type), registerAttribute.Scope);
                }
            }
        }

        private static Func<IServiceProvider, object> GetFactory(Type type)
        {
            ConstructorInfo constructor = type.GetConstructors()[0];
            ParameterInfo[]? parameters = constructor.GetParameters();
            DependencyResolverAttribute?[] dependencyResolvers = new DependencyResolverAttribute[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                dependencyResolvers[i] = parameters[i].GetCustomAttribute<DependencyResolverAttribute>();
            }

            List<SetterInfo> setters = new List<SetterInfo>();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance))
            {
                DependencyResolverAttribute? dependencyResolver = property.GetCustomAttribute<DependencyResolverAttribute>();
                MethodInfo? setter = property.GetSetMethod();

                if (dependencyResolver != null && setter != null && setter.IsPublic)
                {
                    setters.Add(new SetterInfo(property.PropertyType, setter, dependencyResolver));
                }
            }

            return delegate (IServiceProvider serviceProvider)
            {
                object[] arguments = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    arguments[i] = DependencyResolverAttribute.Resolve(
                        serviceProvider,
                        parameters[i].ParameterType,
                        dependencyResolvers[i]);
                }

                object result = constructor.Invoke(arguments);

                foreach (SetterInfo setter in setters)
                {
                    object resolvedDependency = DependencyResolverAttribute.Resolve(
                        serviceProvider,
                        setter.ServiceType,
                        setter.DependencyResolver);

                    setter.Setter.Invoke(result, new[] { resolvedDependency });
                }

                return result;
            };
        }

        private record SetterInfo(Type ServiceType, MethodInfo Setter, DependencyResolverAttribute DependencyResolver);
    }
}
