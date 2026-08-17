# Input and Output

Use `show` to print values and `call input(...)` to ask the user for text.

## Print examples
```acai
show "Hello World"
set name to "Alice"
show "Welcome, {name}!"
```

## Read a string
```acai
set answer to call input("What is your favorite food? ")
show "You answered: {answer}"
```

## Notes
- Strings support interpolation with `{}`.
- Raw strings use `r"..."` to preserve `{}` literally.

- [Introduction](introduction.md)
- [Control Flow](controlflow.md)
