using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Phenix.Core.DependencyInjection
{
    /// <summary>
    /// 服务扩展
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// 注册
        /// </summary>
        public static IServiceCollection AddServices(this IServiceCollection services, Type classType, Func<Type, bool> allow = null)
        {
            ServiceAttribute serviceAttribute = Attribute.GetCustomAttribute(classType, typeof(ServiceAttribute)) as ServiceAttribute;
            if (serviceAttribute != null)
                if (serviceAttribute.InterfaceType == null || serviceAttribute.InterfaceType.IsAssignableFrom(classType))
                    if (allow == null || allow(classType))
                        services.Add(new ServiceDescriptor(serviceAttribute.InterfaceType ?? classType, classType, serviceAttribute.Lifetime));

            return services;
        }

        /// <summary>
        /// 注册
        /// </summary>
        public static IServiceCollection AddServices(this IServiceCollection services, string fileName, Func<Type, bool> allow = null)
        {
            foreach (Type classType in Phenix.Core.Reflection.Utilities.LoadExportedClassTypes(fileName, false))
                AddServices(services, classType, allow);

            return services;
        }

        /// <summary>
        /// 注册
        /// </summary>
        public static IServiceCollection AddServices(this IServiceCollection services, Assembly assembly, Func<Type, bool> allow = null)
        {
            foreach (Type classType in Phenix.Core.Reflection.Utilities.GetExportedClassTypes(assembly, false))
                AddServices(services, classType, allow);

            return services;
        }
    }
}