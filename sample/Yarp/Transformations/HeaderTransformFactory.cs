using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.Transformations;

/// <summary>
/// YARP 请求转换工厂 - 重命名 Header
/// </summary>
public class HeaderTransformFactory : ITransformFactory
{
    /// <summary>
    /// 验证转换配置
    /// </summary>
    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue("RenameHeader", out var header))
        {
            if (string.IsNullOrEmpty(header))
            {
                context.Errors.Add(new ArgumentException("A non-empty RenameHeader value is required"));
            }

            if (transformValues.TryGetValue("Set", out var newHeader))
            {
                if (string.IsNullOrEmpty(newHeader))
                {
                    context.Errors.Add(new ArgumentException("A non-empty Set value is required"));
                }
            }
            else
            {
                context.Errors.Add(new ArgumentException("Set option is required"));
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 构建 Header 重命名转换
    /// </summary>
    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue("RenameHeader", out var header))
        {
            if (string.IsNullOrEmpty(header))
            {
                throw new ArgumentException("A non-empty RenameHeader value is required");
            }

            if (transformValues.TryGetValue("Set", out var newHeader))
            {
                if (string.IsNullOrEmpty(newHeader))
                {
                    throw new ArgumentException("A non-empty Set value is required");
                }
            }
            else
            {
                throw new ArgumentException("Set option is required");
            }

            context.AddRequestTransform(transformContext =>
            {
                if (transformContext.ProxyRequest.Headers.TryGetValues(header, out var headerValue))
                {
                    // Remove the original header
                    transformContext.ProxyRequest.Headers.Remove(header);

                    // Add a new header with the same value(s) as the original header
                    transformContext.ProxyRequest.Headers.Add(newHeader, headerValue);
                }

                return default;
            });

            return true;
        }

        return false;
    }
}
