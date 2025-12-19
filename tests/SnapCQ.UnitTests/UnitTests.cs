using SnapCQ;
using FluentAssertions;

namespace SnapCQ.UnitTests;

public class UnitTests
{
    [Fact]
    public void Unit_Value_IsDefaultInstance()
    {
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        unit1.Should().Be(unit2);
    }

    [Fact]
    public void Unit_DefaultConstructor_CreatesInstance()
    {
        var unit = new Unit();

        unit.Should().Be(Unit.Value);
    }

    [Fact]
    public void Unit_Equality_WorksCorrectly()
    {
        var unit1 = Unit.Value;
        var unit2 = new Unit();
        var unit3 = default(Unit);

        unit1.Should().Be(unit2);
        unit1.Should().Be(unit3);
        unit2.Should().Be(unit3);
    }

    [Fact]
    public void Unit_GetHashCode_ReturnsSameValue()
    {
        var unit1 = Unit.Value;
        var unit2 = new Unit();

        unit1.GetHashCode().Should().Be(unit2.GetHashCode());
    }

    [Fact]
    public void Unit_ToString_ReturnsValue()
    {
        var unit = Unit.Value;

        var result = unit.ToString();

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Unit_CanBeUsedInCollections()
    {
        var list = new List<Unit> { Unit.Value, new Unit(), default };

        list.Should().HaveCount(3);
        list.Distinct().Should().ContainSingle();
    }

    [Fact]
    public void Unit_CanBeUsedAsDictionaryKey()
    {
        var dictionary = new Dictionary<Unit, string>
        {
            { Unit.Value, "test" }
        };

        dictionary[new Unit()].Should().Be("test");
        dictionary[default].Should().Be("test");
    }

    [Fact]
    public void Unit_EqualityOperator_WorksCorrectly()
    {
        var unit1 = Unit.Value;
        var unit2 = new Unit();

        (unit1 == unit2).Should().BeTrue();
        (unit1 != unit2).Should().BeFalse();
    }
}
