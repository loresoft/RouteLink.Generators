namespace RouteLink.Generators.Parsing;

public static class TemplateParameterParser
{
    public static TemplatePart ParseRouteParameter(string parameter)
    {
        if (string.IsNullOrEmpty(parameter))
            return new TemplatePart(string.Empty);

        var startIndex = 0;
        var endIndex = parameter.Length - 1;
        bool isCatchAll = false;
        bool isOptional = false;

        if (parameter.StartsWith("**", StringComparison.Ordinal))
        {
            isCatchAll = true;
            startIndex += 2;
        }
        else if (parameter[0] == '*')
        {
            isCatchAll = true;
            startIndex++;
        }

        if (parameter[endIndex] == '?')
        {
            isOptional = true;
            endIndex--;
        }

        var currentIndex = startIndex;

        // Parse parameter name
        var parameterName = string.Empty;

        while (currentIndex <= endIndex)
        {
            var currentChar = parameter[currentIndex];

            if ((currentChar == ':' || currentChar == '=') && startIndex != currentIndex)
            {
                // Parameter names are allowed to start with delimiters used to denote constraints or default values.
                // i.e. "=foo" or ":bar" would be treated as parameter names rather than default value or constraint
                // specifications.
                parameterName = parameter.Substring(startIndex, currentIndex - startIndex);

                // Roll the index back and move to the constraint parsing stage.
                currentIndex--;
                break;
            }
            else if (currentIndex == endIndex)
            {
                parameterName = parameter.Substring(startIndex, currentIndex - startIndex + 1);
            }

            currentIndex++;
        }

        var constraints = new List<string>();
        currentIndex = ParseConstraints(parameter, currentIndex, endIndex, constraints);

        string? defaultValue = null;
        if (currentIndex <= endIndex &&
            parameter[currentIndex] == '=')
        {
            defaultValue = parameter.Substring(currentIndex + 1, endIndex - currentIndex);
        }

        return new TemplatePart(parameter, parameterName, isCatchAll, isOptional, defaultValue, constraints);
    }

    private static int ParseConstraints(
        string text,
        int currentIndex,
        int endIndex,
        List<string> constraints)
    {
        var state = ParseState.Start;
        var startIndex = currentIndex;
        do
        {
            var currentChar = currentIndex > endIndex ? null : (char?)text[currentIndex];
            switch (state)
            {
                case ParseState.Start:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            break;
                        case ':':
                            state = ParseState.ParsingName;
                            startIndex = currentIndex + 1;
                            break;
                        case '(':
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':
                            state = ParseState.End;
                            currentIndex--;
                            break;
                    }
                    break;
                case ParseState.InsideParenthesis:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            var constraintText = text.Substring(startIndex, currentIndex - startIndex);
                            constraints.Add(constraintText);
                            break;
                        case ')':
                            // Only consume a ')' token if
                            // (a) it is the last token
                            // (b) the next character is the start of the new constraint ':'
                            // (c) the next character is the start of the default value.

                            var nextChar = currentIndex + 1 > endIndex ? null : (char?)text[currentIndex + 1];
                            switch (nextChar)
                            {
                                case null:
                                    state = ParseState.End;
                                    constraintText = text.Substring(startIndex, currentIndex - startIndex + 1);
                                    constraints.Add(constraintText);
                                    break;
                                case ':':
                                    state = ParseState.Start;
                                    constraintText = text.Substring(startIndex, currentIndex - startIndex + 1);
                                    constraints.Add(constraintText);
                                    startIndex = currentIndex + 1;
                                    break;
                                case '=':
                                    state = ParseState.End;
                                    constraintText = text.Substring(startIndex, currentIndex - startIndex + 1);
                                    constraints.Add(constraintText);
                                    break;
                            }
                            break;
                        case ':':
                        case '=':
                            // In the original implementation, the Regex would've backtracked if it encountered an
                            // unbalanced opening bracket followed by (not necessarily immediately) a delimiter.
                            // Simply verifying that the parentheses will eventually be closed should suffice to
                            // determine if the terminator needs to be consumed as part of the current constraint
                            // specification.
                            var indexOfClosingParantheses = text.IndexOf(')', currentIndex + 1);
                            if (indexOfClosingParantheses == -1)
                            {
                                constraintText = text.Substring(startIndex, currentIndex - startIndex);
                                constraints.Add(constraintText);

                                if (currentChar == ':')
                                {
                                    state = ParseState.ParsingName;
                                    startIndex = currentIndex + 1;
                                }
                                else
                                {
                                    state = ParseState.End;
                                    currentIndex--;
                                }
                            }
                            else
                            {
                                currentIndex = indexOfClosingParantheses;
                            }

                            break;
                    }
                    break;
                case ParseState.ParsingName:
                    switch (currentChar)
                    {
                        case null:
                            state = ParseState.End;
                            var constraintText = text.Substring(startIndex, currentIndex - startIndex);
                            if (constraintText.Length > 0)
                            {
                                constraints.Add(constraintText);
                            }
                            break;
                        case ':':
                            constraintText = text.Substring(startIndex, currentIndex - startIndex);
                            if (constraintText.Length > 0)
                            {
                                constraints.Add(constraintText);
                            }
                            startIndex = currentIndex + 1;
                            break;
                        case '(':
                            state = ParseState.InsideParenthesis;
                            break;
                        case '=':
                            state = ParseState.End;
                            constraintText = text.Substring(startIndex, currentIndex - startIndex);
                            if (constraintText.Length > 0)
                            {
                                constraints.Add(constraintText);
                            }
                            currentIndex--;
                            break;
                    }
                    break;
            }

            currentIndex++;

        } while (state != ParseState.End);

        return currentIndex;
    }

    private enum ParseState
    {
        Start,
        ParsingName,
        InsideParenthesis,
        End
    }
}
