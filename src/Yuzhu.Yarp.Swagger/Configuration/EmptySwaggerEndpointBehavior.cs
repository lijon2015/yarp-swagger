namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Controls how Swagger UI is configured when the discovery pipeline finds zero documents.
/// </summary>
public enum EmptySwaggerEndpointBehavior
{
    /// <summary>
    /// Don't register any UI url. The dropdown is empty and a structured warning is logged.
    /// This is the default and matches the long-term plan: the UI must not advertise
    /// documents that don't exist.
    /// </summary>
    NoEndpoints = 0,

    /// <summary>
    /// Register a single diagnostic endpoint named "Swagger Aggregation Diagnostics" that
    /// serves a JSON description of the empty discovery state. Useful in development to
    /// surface why the dropdown is empty without inventing a fake API document.
    /// </summary>
    DiagnosticEndpoint = 1,
}
