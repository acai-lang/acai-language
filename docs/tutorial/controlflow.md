# Control Flow

Acai supports readable conditional blocks and loops.

## `if` with `else if`
```acai
set x to 5
if x < 3 then
    show "x is less than 3"
else if x == 5 then
    show "x equals five"
else
    show "x is something else"
end
```

## `repeat` loops
```acai
set count to 0
repeat until count == 3
    set count = count + 1
    show "Loop {count}"
end
```

## `for` loops
```acai
for i from 1 to 5
    show "Number {i}"
end
```

- [Input / Output](inputoutput.md)
- [Data Structures](datastructure.md)
