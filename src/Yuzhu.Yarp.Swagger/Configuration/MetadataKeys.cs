namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// YARP Cluster Metadata 键常量
/// </summary>
public static class MetadataKeys
{
    /// <summary>
    /// 是否启用 Swagger 聚合
    /// </summary>
    public const string Enabled = "Swagger:Enabled";

    /// <summary>
    /// Swagger 文档路径
    /// </summary>
    public const string Path = "Swagger:Path";

    /// <summary>
    /// 路径前缀
    /// </summary>
    public const string Prefix = "Swagger:Prefix";

    /// <summary>
    /// 路径过滤正则表达式
    /// </summary>
    public const string PathFilter = "Swagger:PathFilter";

    /// <summary>
    /// 是否只包含已发布的路径
    /// </summary>
    public const string OnlyPublishedPaths = "Swagger:OnlyPublishedPaths";

    /// <summary>
    /// 是否作为元数据源
    /// </summary>
    public const string IsMetadataSource = "Swagger:IsMetadataSource";

    /// <summary>
    /// 访问令牌客户端名称
    /// </summary>
    public const string AccessTokenClient = "Swagger:AccessTokenClient";

    /// <summary>
    /// 文档名称（用于分组）
    /// </summary>
    public const string DocumentName = "Swagger:DocumentName";
}
