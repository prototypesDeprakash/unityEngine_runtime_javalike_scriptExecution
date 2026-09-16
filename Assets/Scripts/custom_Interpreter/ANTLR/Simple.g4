grammar Simple;

// ============================================================
// PROGRAM
// ============================================================

program
    : topLevelItem* EOF
    ;

topLevelItem
    : functionDeclaration
    | statement
    | ifBlock
    | whileBlock
    | forBlock
    | forEachBlock
    | doWhileBlock
    | switchBlock
    ;

// ============================================================
// FUNCTIONS
// ============================================================

// public void myscript() { }
// private int add(int a, int b) { return a + b; }
// void test() { }

functionDeclaration
    : accessModifier? returnType ID '(' parameterList? ')' block
    ;

accessModifier
    : PUBLIC
    | PRIVATE
    ;

returnType
    : VOID
    | type
    ;

parameterList
    : parameter (',' parameter)*
    ;

parameter
    : type variableDeclaratorId
    ;

// ============================================================
// BLOCKS
// ============================================================

block
    : '{' blockItem* '}'
    ;

blockItem
    : statement
    | ifBlock
    | whileBlock
    | forBlock
    | forEachBlock
    | doWhileBlock
    | switchBlock
    ;

// ============================================================
// STATEMENTS
// ============================================================

statement
    : variableDeclaration
    | expressionStatement
    | printStatement
    | returnStatement
    | breakStatement
    | continueStatement
    ;

// ============================================================
// EXPRESSION STATEMENT
// ============================================================

// x = 5;
// arr[i] = 10;
// foo();
// x++;

expressionStatement
    : expression ';'
    ;

// ============================================================
// VARIABLE DECLARATION
// ============================================================

// int x = 10;
// int[] arr = new int[10];
// int[] arr = new int[] {1, 2, 3};
// int arr[] = new int[10];
// boolean[] flags;
// String names[];

variableDeclaration
    : type variableDeclarator (',' variableDeclarator)* ';'
    ;

variableDeclarator
    : variableDeclaratorId ('=' expression)?
    ;

variableDeclaratorId
    : ID arrayDimensions?
    ;

// ============================================================
// TYPES
// ============================================================

// int
// boolean
// double
// float
// long
// short
// byte
// char
// String
//
// and arrays:
//
// int[]
// int[][]
// boolean[]
// String[][]

type
    : baseType arrayDimensions?
    ;

baseType
    : INT_TYPE
    | LONG_TYPE
    | DOUBLE_TYPE
    | FLOAT_TYPE
    | SHORT_TYPE
    | BYTE_TYPE
    | BOOLEAN_TYPE
    | CHAR_TYPE
    | STRING_TYPE
    ;

arrayDimensions
    : ('[' ']')+
    ;

// ============================================================
// EXPRESSIONS
// ============================================================

expression
    : assignmentExpression
    ;

// ============================================================
// ASSIGNMENT
// ============================================================

// x = 10
// x += 5
// arr[i] = 20
// arr[i] *= 2

assignmentExpression
    : conditionalExpression
    | assignmentTarget assignmentOperator assignmentExpression
    ;

assignmentTarget
    : qualifiedName
    | arrayAccess
    ;

assignmentOperator
    : '='
    | '+='
    | '-='
    | '*='
    | '/='
    | '%='
    | '&='
    | '|='
    | '^='
    | '<<='
    | '>>='
    | '>>>='
    ;

// ============================================================
// TERNARY
// ============================================================

// x > 5 ? 10 : 20

conditionalExpression
    : logicalOrExpression
    | logicalOrExpression '?' expression ':' conditionalExpression
    ;

// ============================================================
// LOGICAL OR
// ============================================================

logicalOrExpression
    : logicalAndExpression
    | logicalOrExpression '||' logicalAndExpression
    ;

// ============================================================
// LOGICAL AND
// ============================================================

logicalAndExpression
    : bitwiseOrExpression
    | logicalAndExpression '&&' bitwiseOrExpression
    ;

// ============================================================
// BITWISE OR
// ============================================================

// a | b

bitwiseOrExpression
    : bitwiseXorExpression
    | bitwiseOrExpression '|' bitwiseXorExpression
    ;

// ============================================================
// BITWISE XOR
// ============================================================

// a ^ b

bitwiseXorExpression
    : bitwiseAndExpression
    | bitwiseXorExpression '^' bitwiseAndExpression
    ;

// ============================================================
// BITWISE AND
// ============================================================

// a & b

bitwiseAndExpression
    : equalityExpression
    | bitwiseAndExpression '&' equalityExpression
    ;

// ============================================================
// EQUALITY
// ============================================================

// == !=

equalityExpression
    : relationalExpression
    | equalityExpression '==' relationalExpression
    | equalityExpression '!=' relationalExpression
    ;

// ============================================================
// RELATIONAL
// ============================================================

// < > <= >=

relationalExpression
    : shiftExpression
    | relationalExpression '<' shiftExpression
    | relationalExpression '>' shiftExpression
    | relationalExpression '<=' shiftExpression
    | relationalExpression '>=' shiftExpression
    ;

// ============================================================
// SHIFT
// ============================================================

// << >> >>>

shiftExpression
    : additiveExpression
    | shiftExpression '<<' additiveExpression
    | shiftExpression '>>' additiveExpression
    | shiftExpression '>>>' additiveExpression
    ;

// ============================================================
// ADDITION / SUBTRACTION
// ============================================================

additiveExpression
    : multiplicativeExpression
    | additiveExpression '+' multiplicativeExpression
    | additiveExpression '-' multiplicativeExpression
    ;

// ============================================================
// MULTIPLICATION / DIVISION / MODULO
// ============================================================

multiplicativeExpression
    : unaryExpression
    | multiplicativeExpression '*' unaryExpression
    | multiplicativeExpression '/' unaryExpression
    | multiplicativeExpression '%' unaryExpression
    ;

// ============================================================
// UNARY
// ============================================================

// !x
// +x
// -x
// ~x
// ++x
// --x

unaryExpression
    : postfixExpression
    | '!' unaryExpression
    | '+' unaryExpression
    | '-' unaryExpression
    | '~' unaryExpression
    | '++' assignmentTarget
    | '--' assignmentTarget
    ;

// ============================================================
// POSTFIX
// ============================================================

// x++
// x--
// arr[i]
// arr[i][j]
// arr.length

postfixExpression
    : primaryExpression
    | postfixExpression '++'
    | postfixExpression '--'
    | postfixExpression '[' expression ']'
    | postfixExpression '.' ID
    ;

// ============================================================
// PRIMARY EXPRESSIONS
// ============================================================

primaryExpression
    : literal
    | functionCall
    | qualifiedName
    | arrayCreation
    | arrayInitializer
    | '(' expression ')'
    ;

// ============================================================
// FUNCTION CALLS
// ============================================================

// move()
// move(5)
// getWorldSize()
// setPosition(x, y)

functionCall
    : ID '(' argumentList? ')'
    ;

argumentList
    : expression (',' expression)*
    ;

// ============================================================
// NAMES
// ============================================================

// x
// worldSize
// Entities.Pumpkin
// Items.Water
// Grounds.Soil

qualifiedName
    : ID ('.' ID)*
    ;

// ============================================================
// ARRAY CREATION
// ============================================================

// new int[10]
// new boolean[10]
// new double[20]
//
// new int[3][4]
// new boolean[3][4]
//
// new int[] {1, 2, 3}
// new boolean[] {true, false, true}
// new String[] {"A", "B"}

arrayCreation
    : NEW baseType arrayCreationDimensions arrayInitializer?
    ;

arrayCreationDimensions
    : arrayDimension arrayDimension*
    ;

arrayDimension
    : '[' expression ']'
    | '[' ']'
    ;

// ============================================================
// ARRAY INITIALIZER
// ============================================================

// {1, 2, 3}
// {true, false, true}
// {"A", "B"}

arrayInitializer
    : '{' expressionList? '}'
    ;

expressionList
    : expression (',' expression)*
    ;

// ============================================================
// ARRAY ACCESS
// ============================================================

// arr[0]
// arr[i]
// arr[i][j]

arrayAccess
    : qualifiedName ('[' expression ']')+
    ;

// ============================================================
// PRINT
// ============================================================

// Special language feature.
// Not a real Java class/object system.

printStatement
    : PRINTLN '(' expression? ')' ';'
    | PRINT '(' expression? ')' ';'
    ;

// ============================================================
// RETURN
// ============================================================

returnStatement
    : RETURN expression? ';'
    ;

// ============================================================
// BREAK / CONTINUE
// ============================================================

breakStatement
    : BREAK ';'
    ;

continueStatement
    : CONTINUE ';'
    ;

// ============================================================
// IF / ELSE
// ============================================================

ifBlock
    : IF '(' expression ')' block
      (ELSE (ifBlock | block))?
    ;

// ============================================================
// WHILE
// ============================================================

whileBlock
    : WHILE '(' expression ')' block
    ;

// ============================================================
// FOR
// ============================================================

// for (int i = 0; i < 10; i++)
// for (; i < 10; i++)
// for (i = 0; i < 10; i++)

forBlock
    : FOR '(' forInit? ';' expression? ';' forUpdate? ')' block
    ;

forInit
    : variableDeclarationNoSemicolon
    | expressionList
    ;

variableDeclarationNoSemicolon
    : type variableDeclarator (',' variableDeclarator)*
    ;

forUpdate
    : expressionList
    ;

// ============================================================
// FOR-EACH
// ============================================================

// for (int x : arr)
// for (String s : names)

forEachBlock
    : FOR '(' type ID ':' expression ')' block
    ;

// ============================================================
// DO-WHILE
// ============================================================

// do { ... } while (x < 10);

doWhileBlock
    : DO block WHILE '(' expression ')' ';'
    ;

// ============================================================
// SWITCH
// ============================================================

// switch (x) {
//     case 1:
//         ...
//         break;
//     case Entities.Pumpkin:
//         ...
//         break;
//     default:
//         ...
// }

switchBlock
    : SWITCH '(' expression ')' '{' switchCase* defaultCase? '}'
    ;

switchCase
    : CASE (literal | qualifiedName) ':' blockItem*
    ;

defaultCase
    : DEFAULT ':' blockItem*
    ;

// ============================================================
// LITERALS
// ============================================================

literal
    : INT
    | LONG
    | DOUBLE
    | FLOAT
    | STRING
    | CHAR
    | TRUE
    | FALSE
    | NULL
    ;

// ============================================================
// KEYWORDS
// ============================================================

PUBLIC
    : 'public'
    ;

PRIVATE
    : 'private'
    ;

VOID
    : 'void'
    ;

IF
    : 'if'
    ;

ELSE
    : 'else'
    ;

WHILE
    : 'while'
    ;

FOR
    : 'for'
    ;

RETURN
    : 'return'
    ;

BREAK
    : 'break'
    ;

CONTINUE
    : 'continue'
    ;

NEW
    : 'new'
    ;

DO
    : 'do'
    ;

SWITCH
    : 'switch'
    ;

CASE
    : 'case'
    ;

DEFAULT
    : 'default'
    ;

TRUE
    : 'true'
    ;

FALSE
    : 'false'
    ;

NULL
    : 'null'
    ;

INT_TYPE
    : 'int'
    ;

LONG_TYPE
    : 'long'
    ;

DOUBLE_TYPE
    : 'double'
    ;

FLOAT_TYPE
    : 'float'
    ;

SHORT_TYPE
    : 'short'
    ;

BYTE_TYPE
    : 'byte'
    ;

BOOLEAN_TYPE
    : 'boolean'
    ;

CHAR_TYPE
    : 'char'
    ;

STRING_TYPE
    : 'String'
    ;

// ============================================================
// SPECIAL PRINT TOKENS
// ============================================================

PRINTLN
    : 'System.out.println'
    ;

PRINT
    : 'System.out.print'
    ;

// ============================================================
// NUMERIC LITERALS
// ============================================================

DOUBLE
    : [0-9]+ '.' [0-9]+
    ;

FLOAT
    : [0-9]+ '.' [0-9]+ [fF]
    ;

LONG
    : [0-9]+ [lL]
    ;

INT
    : [0-9]+
    ;

// ============================================================
// CHARACTER
// ============================================================

CHAR
    : '\'' (~['\\\r\n] | '\\' .) '\''
    ;

// ============================================================
// STRING
// ============================================================

STRING
    : '"' (~["\\\r\n] | '\\' .)* '"'
    ;

// ============================================================
// IDENTIFIER
// ============================================================

ID
    : [a-zA-Z_][a-zA-Z_0-9]*
    ;

// ============================================================
// COMMENTS
// ============================================================

LINE_COMMENT
    : '//' ~[\r\n]* -> skip
    ;

BLOCK_COMMENT
    : '/*' .*? '*/' -> skip
    ;

// ============================================================
// WHITESPACE
// ============================================================

WS
    : [ \t\r\n]+ -> skip
    ;