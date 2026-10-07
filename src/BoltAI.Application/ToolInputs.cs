namespace BoltAI.Application;
public sealed record OrderToolInput(string OrderNumber);
public sealed record ProductToolInput(string ProductCode);
public sealed record ShipmentToolInput(string ShipmentId);
public sealed record DocumentSearchInput(string Query);
public sealed record OrderHistoryInput;
