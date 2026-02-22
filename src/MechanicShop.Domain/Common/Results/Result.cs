using System.ComponentModel;
using System.Text.Json.Serialization;

using MechanicShop.Domain.Common.Results.Abstractions;

namespace MechanicShop.Domain.Common.Results;

/*
 * In case When I don't want return real value.
 * These are empty value types (no properties, no data).
 * They exist only to represent a successful outcome with a semantic meaning.
 */

public readonly record struct Success;

public readonly record struct Created;

public readonly record struct Deleted;

public readonly record struct Updated;

/*
 * This is a factory shortcut.
 * Instead of writing: return new Success(); => return Result.Success;
 * These are empty value types (no properties, no data).
 * They exist only to represent a successful outcome with a semantic meaning.
 */

public static class Result
{
    public static Success Success => default; // equivalent to : new Success();
    public static Created Created => default;
    public static Deleted Deleted => default;
    public static Updated Updated => default;
}

public sealed class Result<TValue> : IResult<TValue>
{
    private readonly TValue _value = default!;

    private readonly List<Error>? _errors;

    /*
     * [JsonConstructor]
     * The serializer needs a public constructor to rebuild the object.
     * But you don’t want developers to use it manually.
     * Tells System.Text.Json: “Use this constructor when deserializing JSON.”
     *
     * [EditorBrowsable(EditorBrowsableState.Never)]
     * Hides this constructor, So developers don’t accidentally use it.
     */
    [JsonConstructor]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("For serializer only.", true)]
    public Result(TValue? value, List<Error>? errors, bool isSuccess)
    {
        if (isSuccess)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _errors = [];
            IsSuccess = true;
        }

        else
        {
            if (errors == null || errors.Count == 0)
            {
                throw new ArgumentException("Provide at least one error.", nameof(errors));
            }

            _errors = errors;
            _value = default!;
            IsSuccess = false;
        }
    }

    private Result(Error error)
    {
        _errors = [error];
        
        IsSuccess = false;
    }

    private Result(List<Error> errors)
    {
        if (errors is null || errors.Count == 0)
        {
            throw new ArgumentException(
                "Cannot create an ErrorOr<TValue> from an empty collection of errors. Provide at least one error.",
                nameof(errors));
        }

        _errors = errors;

        IsSuccess = false;
    }

    private Result(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _value = value;

        IsSuccess = true;
    }

    public bool IsSuccess { get; }

    public bool IsError => !IsSuccess;

    public List<Error> Errors => IsError ? _errors! : [];

    public TValue Value => IsSuccess ? _value : default!;

    public Error TopError => _errors?.Count > 0 ? _errors[0] : default;

    /*
     * This replaces if / else or switch.
     * Instead of this (or switch) ❌
        if (result.IsSuccess) return Ok(result.Value);
        else return BadRequest(result.Errors);

     * You write this ✅
      return result.Match(value  => Ok(value),  errors => BadRequest(errors));
     */
    public TNextValue Match<TNextValue>(Func<TValue, TNextValue> onValue, Func<List<Error>, TNextValue> onError)
        => IsSuccess ? onValue(Value!) : onError(Errors);

    /*
     * Implicit conversion operators
     *
     * Without implicit operators ❌
     *  return new Result<User>(user);
     *
     * With implicit operators ✅
     *  return user;
     *
     * The compiler automatically converts them into Result<T>.
     */
    public static implicit operator Result<TValue>(TValue value)
        => new(value);

    public static implicit operator Result<TValue>(Error error)
        => new(error);

    public static implicit operator Result<TValue>(List<Error> errors)
        => new(errors);
}