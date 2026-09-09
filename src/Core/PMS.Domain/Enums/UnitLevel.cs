namespace PMS.Domain.Enums;

/// <summary>
/// Which of a product's three possible selling units a quantity is expressed in.
///
/// A product always defines <see cref="Base"/>. <see cref="Mid"/> and <see cref="Large"/> are
/// optional, and a quantity in a level the product does not define is a programming error
/// rather than a validation message — see the unit conversion helpers.
/// </summary>
public enum UnitLevel
{
    /// <summary>The individual item a customer can buy: a piece, a bottle, a tin, a bag.</summary>
    Base = 0,

    /// <summary>A middle pack, typically a strip.</summary>
    Mid = 1,

    /// <summary>A bulk pack: a box, a carton.</summary>
    Large = 2,
}
